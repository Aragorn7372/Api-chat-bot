using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiChatbot.Background;

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

    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

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

    private sealed record OllamaWarmupRequest
    {
        public string Model { get; init; } = string.Empty;
        public string Prompt { get; init; } = string.Empty;
        public bool Stream { get; init; }
    }
}
