namespace ApiChatbot.Domain;

/// <summary>
/// DTO que representa una entrada de analytics para una petición de chat.
/// Los datos sensibles (IP, session ID) se almacenan con hash SHA-256.
/// </summary>
public class AnalyticsEntry
{
    /// <summary>
    /// Hash SHA-256 del identificador de sesión.
    /// </summary>
    public string SessionIdHash { get; init; } = string.Empty;

    /// <summary>
    /// Hash SHA-256 de la dirección IP del cliente.
    /// </summary>
    public string IpHash { get; init; } = string.Empty;

    /// <summary>
    /// Mensaje original enviado por el usuario.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Respuesta generada por el chatbot.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Fecha y hora UTC de la petición.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Tiempo de respuesta del proveedor de IA en milisegundos.
    /// </summary>
    public long ResponseTimeMs { get; init; }
}

/// <summary>
/// Interfaz para el registro de analytics de peticiones de chat.
/// Permite registrar consultas, respuestas y métricas de rendimiento.
/// </summary>
public interface IAnalyticsLogger
{
    /// <summary>
    /// Registra una entrada de analytics de forma asíncrona.
    /// </summary>
    /// <param name="entry">Datos de la petición a registrar.</param>
    Task LogAsync(AnalyticsEntry entry);
}
