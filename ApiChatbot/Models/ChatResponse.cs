namespace ApiChatbot.Models;

/// <summary>
/// DTO de salida para el endpoint de chat.
/// Contiene la respuesta generada por el chatbot.
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Respuesta generada por el modelo de IA.
    /// </summary>
    public string Reply { get; init; } = string.Empty;
}
