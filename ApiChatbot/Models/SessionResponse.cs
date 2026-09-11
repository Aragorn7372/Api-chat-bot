namespace ApiChatbot.Models;

/// <summary>
/// DTO de salida para el endpoint de creación de sesión.
/// Contiene el token JWT y su fecha de expiración.
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// Token JWT para autenticar las peticiones de chat.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Fecha y hora de expiración del token en UTC.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}
