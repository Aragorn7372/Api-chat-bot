using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiChatbot.Background;

/// <summary>
/// Servicio en segundo plano que pre-carga el modelo de Ollama al iniciar la aplicación.
/// Espera a que Ollama esté disponible (hasta 30 intentos con 2 segundos de intervalo)
/// y luego envía una petición de warmup para cargar el modelo en memoria,
/// reduciendo la latencia de la primera petición real.
/// </summary>
public class OllamaWarmupService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaWarmupService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="OllamaWarmupService"/>.
    /// </summary>
    /// <param name="httpClientFactory">Factory para crear el cliente HTTP "Ollama".</param>
    /// <param name="configuration">Configuración de la aplicación para URL y modelo de Ollama.</param>
    /// <param name="logger">Logger para registro de eventos.</param>
    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el warmup de Ollama: espera disponibilidad y carga el modelo.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelación para detener el servicio.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _configuration["Ollama:Model"] ?? "qwen2.5:1.5b";

        _logger.LogInformation("Iniciando warmup de Ollama: {BaseUrl} | Modelo: {Model}", baseUrl, model);

        using var client = _httpClientFactory.CreateClient("Ollama");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));

        if (!await WaitForOllamaAsync(client, stoppingToken))
        {
            _logger.LogWarning("No se pudo contactar con Ollama, el warmup continuará en segundo plano");
            return;
        }

        await LoadModelAsync(client, model, stoppingToken);
    }

    /// <summary>
    /// Espera a que Ollama esté disponible consultando el endpoint /api/tags.
    /// </summary>
    /// <param name="client">Cliente HTTP configurado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><c>true</c> si Ollama respondió; <c>false</c> si se agotaron los intentos.</returns>
    private async Task<bool> WaitForOllamaAsync(HttpClient client, CancellationToken ct)
    {
        for (var i = 0; i < 30; i++)
        {
            try
            {
                var response = await client.GetAsync("/api/tags", ct);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ollama disponible tras {Intentos} intentos", i + 1);
                    return true;
                }
            }
            catch
            {
                // Ollama aún no responde
            }

            _logger.LogInformation("Esperando a Ollama... intento {Intento}/30", i + 1);
            await Task.Delay(2000, ct);
        }

        return false;
    }

    /// <summary>
    /// Envía una petición de warmup a Ollama para pre-cargar el modelo en memoria.
    /// </summary>
    /// <param name="client">Cliente HTTP configurado.</param>
    /// <param name="model">Nombre del modelo a cargar.</param>
    /// <param name="ct">Token de cancelación.</param>
    private async Task LoadModelAsync(HttpClient client, string model, CancellationToken ct)
    {
        try
        {
            var request = new OllamaWarmupRequest { Model = model, Prompt = "hello", Stream = false };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.PostAsync("/api/generate", content, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Modelo {Model} cargado en {ElapsedMs}ms", model, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Error al cargar modelo {Model}: {StatusCode}", model, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error durante warmup del modelo {Model}", model);
        }
    }

    /// <summary>
    /// Petición de warmup enviada a Ollama para pre-cargar el modelo.
    /// </summary>
    private sealed record OllamaWarmupRequest
    {
        /// <summary>Nombre del modelo a cargar.</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>Prompt de prueba para activar la carga del modelo.</summary>
        public string Prompt { get; init; } = string.Empty;

        /// <summary>Si es <c>false</c>, espera la respuesta completa.</summary>
        public bool Stream { get; init; }
    }
}
