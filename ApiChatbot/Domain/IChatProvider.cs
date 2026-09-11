namespace ApiChatbot.Domain;

/// <summary>
/// Representa un mensaje en la conversación con el chatbot.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Rol del mensaje: "user", "assistant" o "system".
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Contenido textual del mensaje.
    /// </summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Interfaz para proveedores de inteligencia artificial.
/// Define el contrato para generar respuestas basadas en un prompt del sistema
/// y el historial de conversación.
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// Genera una respuesta del modelo de IA basada en el contexto proporcionado.
    /// </summary>
    /// <param name="systemPrompt">Prompt del sistema que define el comportamiento del chatbot.</param>
    /// <param name="conversation">Historial de la conversación actual.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Texto de la respuesta generada por el modelo.</returns>
    Task<string> GenerateResponseAsync(string systemPrompt, List<ChatMessage> conversation, CancellationToken ct = default);
}
