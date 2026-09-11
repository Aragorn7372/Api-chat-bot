using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiChatbot.Domain;

/// <summary>
/// Implementación de <see cref="IChatProvider"/> para Ollama (LLM local).
/// Realiza peticiones HTTP al endpoint /api/chat de Ollama para generar respuestas.
/// </summary>
public class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly double _temperature;
    private readonly ILogger<OllamaChatProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="OllamaChatProvider"/>.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP para comunicarse con Ollama.</param>
    /// <param name="baseUrl">URL base del servidor Ollama (ej: http://localhost:11434).</param>
    /// <param name="model">Nombre del modelo a utilizar (ej: qwen2.5:1.5b).</param>
    /// <param name="temperature">Temperatura del modelo (0.0 - 1.0). Mayor valor = más creatividad.</param>
    /// <param name="logger">Logger para registro de eventos.</param>
    public OllamaChatProvider(HttpClient httpClient, string baseUrl, string model, double temperature, ILogger<OllamaChatProvider> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _model = model;
        _temperature = temperature;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateResponseAsync(string systemPrompt, List<ChatMessage> conversation, CancellationToken ct = default)
    {
        var messages = new List<OllamaMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var msg in conversation)
        {
            messages.Add(new OllamaMessage { Role = msg.Role, Content = msg.Content });
        }

        var request = new OllamaChatRequest
        {
            Model = _model,
            Messages = messages,
            Stream = false,
            Options = new OllamaOptions { Temperature = _temperature }
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("LLM Request: POST {BaseUrl}/api/chat | Modelo: {Model} | Mensajes: {Count}", _httpClient.BaseAddress, _model, messages.Count);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/api/chat", content, ct);
            _logger.LogInformation("LLM Response: {StatusCode}", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM Error al llamar a Ollama en {BaseUrl}/api/chat", _httpClient.BaseAddress);
            throw;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, JsonOptions);

        var replyContent = result?.Message?.Content;
        return string.IsNullOrEmpty(replyContent) ? "Lo siento, no pude generar una respuesta." : replyContent;
    }
}

/// <summary>
/// Representa un mensaje en el formato de la API de Ollama.
/// </summary>
internal sealed record OllamaMessage
{
    /// <summary>Rol del mensaje (system, user, assistant).</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Contenido del mensaje.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Petición de chat enviada a la API de Ollama.
/// </summary>
internal sealed record OllamaChatRequest
{
    /// <summary>Nombre del modelo a utilizar.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Lista de mensajes de la conversación.</summary>
    public List<OllamaMessage> Messages { get; init; } = [];

    /// <summary>Si es <c>true</c>, streaming de respuesta. <c>false</c> = respuesta completa.</summary>
    public bool Stream { get; init; }

    /// <summary>Opciones de generación del modelo.</summary>
    public OllamaOptions Options { get; init; } = new();
}

/// <summary>
/// Opciones de configuración para la generación de respuestas.
/// </summary>
internal sealed record OllamaOptions
{
    /// <summary>Temperatura del modelo (0.0 - 1.0).</summary>
    public double Temperature { get; init; }
}

/// <summary>
/// Respuesta recibida de la API de Ollama.
/// </summary>
internal sealed record OllamaChatResponse
{
    /// <summary>Mensaje generado por el modelo.</summary>
    public OllamaMessage? Message { get; init; }

    /// <summary>Indica si la generación ha finalizado.</summary>
    public bool Done { get; init; }
}
