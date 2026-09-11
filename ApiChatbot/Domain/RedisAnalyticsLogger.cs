using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace ApiChatbot.Domain;

public class RedisAnalyticsLogger : IAnalyticsLogger, IAsyncDisposable
{
    private readonly ConnectionMultiplexer? _redis;
    private readonly int _ttlDays;
    private readonly ILogger<RedisAnalyticsLogger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    public async ValueTask DisposeAsync()
    {
        if (_redis is not null)
            await _redis.CloseAsync();
    }
}

public static class HashHelper
{
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
