namespace ApiChatbot.Domain;

/// <summary>
/// Interfaz para el almacenamiento de sesiones y conversaciones.
/// Define las operaciones de rate limiting y persistencia del historial de chat.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Verifica si una sesión ha excedido el límite de peticiones permitidas.
    /// </summary>
    /// <param name="sessionId">Identificador único de la sesión.</param>
    /// <returns><c>true</c> si la sesión está limitada; <c>false</c> en caso contrario.</returns>
    bool IsRateLimited(string sessionId);

    /// <summary>
    /// Registra una nueva petición para control de rate limiting.
    /// </summary>
    /// <param name="sessionId">Identificador único de la sesión.</param>
    void RecordRequest(string sessionId);

    /// <summary>
    /// Obtiene el historial de conversación completo de una sesión.
    /// </summary>
    /// <param name="sessionId">Identificador único de la sesión.</param>
    /// <returns>Lista de mensajes de la conversación.</returns>
    List<ChatMessage> GetConversation(string sessionId);

    /// <summary>
    /// Agrega un mensaje al historial de conversación de una sesión.
    /// Si el historial excede el límite, se eliminan los mensajes más antiguos.
    /// </summary>
    /// <param name="sessionId">Identificador único de la sesión.</param>
    /// <param name="message">Mensaje a agregar.</param>
    void AddMessage(string sessionId, ChatMessage message);
}
