namespace ApiChatbot.Models;

/// <summary>
/// DTO de entrada para el endpoint de chat.
/// Contiene el mensaje que el usuario desea enviar al chatbot.
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// Mensaje del usuario. No puede estar vacío ni exceder 2000 caracteres.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
