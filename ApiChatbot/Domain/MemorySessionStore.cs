using System.Collections.Concurrent;

namespace ApiChatbot.Domain;

/// <summary>
/// Implementación en memoria de <see cref="ISessionStore"/>.
/// Utiliza <see cref="ConcurrentDictionary{TKey,TValue}"/> para almacenar sesiones
/// de forma thread-safe con bloqueo explícito por sesión.
/// </summary>
/// <remarks>
/// Limitaciones:
/// <list type="bullet">
///   <item><description>Las sesiones se pierden al reiniciar la aplicación</description></item>
///   <item><description>No es adequado para escenarios multi-instancia sin un store distribuido</description></item>
/// </list>
/// </remarks>
public class MemorySessionStore : ISessionStore
{
    private static readonly ConcurrentDictionary<string, SessionData> Sessions = new();
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private const int MaxRequestsPerWindow = 30;
    private const int MaxConversationHistory = 3;

    /// <inheritdoc />
    public bool IsRateLimited(string sessionId)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            var now = DateTime.UtcNow;
            data.RequestTimestamps.RemoveAll(t => now - t > RateLimitWindow);
            return data.RequestTimestamps.Count >= MaxRequestsPerWindow;
        }
    }

    /// <inheritdoc />
    public void RecordRequest(string sessionId)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            data.RequestTimestamps.Add(DateTime.UtcNow);
        }
    }

    /// <inheritdoc />
    public List<ChatMessage> GetConversation(string sessionId)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            return [..data.Messages];
        }
    }

    /// <inheritdoc />
    public void AddMessage(string sessionId, ChatMessage message)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            data.Messages.Add(message);
            if (data.Messages.Count > MaxConversationHistory * 2)
                data.Messages.RemoveRange(0, data.Messages.Count - MaxConversationHistory * 2);
        }
    }

    /// <summary>
    /// Clase interna que encapsula los datos de una sesión individual.
    /// </summary>
    private sealed class SessionData
    {
        /// <summary>
        /// Objeto de bloqueo para operaciones thread-safe por sesión.
        /// </summary>
        public readonly object Lock = new();

        /// <summary>
        /// Lista de timestamps de peticiones para control de rate limiting.
        /// </summary>
        public List<DateTime> RequestTimestamps { get; } = [];

        /// <summary>
        /// Historial de mensajes de la conversación.
        /// </summary>
        public List<ChatMessage> Messages { get; } = [];
    }
}
