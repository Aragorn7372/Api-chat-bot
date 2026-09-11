using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace ApiChatbot.Domain;

/// <summary>
/// Implementación de <see cref="IAnalyticsLogger"/> que almacena entradas de analytics en Redis.
/// Los datos sensibles (IP, session ID) se almacenan con hash SHA-256 por privacidad.
/// </summary>
/// <remarks>
/// Características:
/// <list type="bullet">
///   <item><description>Degradación graceful: si Redis no está disponible, simplemente no registra</description></item>
///   <item><description>TTL configurable para auto-limpieza de datos (default: 14 días)</description></item>
///   <item><description>Claves con formato: chatbot:log:{fecha}:{sessionHash}:{guid}</description></item>
/// </list>
/// </remarks>
public class RedisAnalyticsLogger : IAnalyticsLogger, IAsyncDisposable
{
    private readonly ConnectionMultiplexer? _redis;
    private readonly int _ttlDays;
    private readonly ILogger<RedisAnalyticsLogger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="RedisAnalyticsLogger"/>.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión a Redis. Si es nula o vacía, el logging se deshabilita.</param>
    /// <param name="ttlDays">Días de retención de los logs en Redis.</param>
    /// <param name="logger">Logger para registro de eventos.</param>
    public RedisAnalyticsLogger(string? connectionString, int ttlDays, ILogger<RedisAnalyticsLogger> logger)
    {
        _ttlDays = ttlDays;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Redis no configurado. Logging de analytics deshabilitado.");
            return;
        }

        try
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _logger.LogInformation("Conectado a Redis para analytics logging");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar a Redis. Logging deshabilitado.");
        }
    }

    /// <inheritdoc />
    public async Task LogAsync(AnalyticsEntry entry)
    {
        if (_redis is null) return;

        try
        {
            var db = _redis.GetDatabase();
            var key = $"chatbot:log:{entry.Timestamp:yyyy-MM-dd}:{entry.SessionIdHash}:{Guid.NewGuid():N}";
            var value = JsonSerializer.Serialize(entry, JsonOptions);
            await db.StringSetAsync(key, value, TimeSpan.FromDays(_ttlDays));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al guardar log en Redis");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_redis is not null)
            await _redis.CloseAsync();
    }
}

/// <summary>
/// Utilidad estática para generar hashes SHA-256.
/// Se usa para hashear datos sensibles (IP, session ID) antes de almacenarlos.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Genera un hash SHA-256 de la cadena de entrada.
    /// </summary>
    /// <param name="input">Cadena a hashear.</param>
    /// <returns>Hash SHA-256 en formato hexadecimal minúsculas.</returns>
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
