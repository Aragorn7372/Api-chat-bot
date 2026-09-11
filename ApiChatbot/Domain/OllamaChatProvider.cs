using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiChatbot.Domain;

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

    public OllamaChatProvider(HttpClient httpClient, string baseUrl, string model, double temperature, ILogger<OllamaChatProvider> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _model = model;
        _temperature = temperature;
        _logger = logger;
    }

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

internal sealed record OllamaMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

internal sealed record OllamaChatRequest
{
    public string Model { get; init; } = string.Empty;
    public List<OllamaMessage> Messages { get; init; } = [];
    public bool Stream { get; init; }
    public OllamaOptions Options { get; init; } = new();
}

internal sealed record OllamaOptions
{
    public double Temperature { get; init; }
}

internal sealed record OllamaChatResponse
{
    public OllamaMessage? Message { get; init; }
    public bool Done { get; init; }
}
