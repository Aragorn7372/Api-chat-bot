using System.Collections.Concurrent;

namespace ApiChatbot.Domain;

public class MemorySessionStore : ISessionStore
{
    private static readonly ConcurrentDictionary<string, SessionData> Sessions = new();
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private const int MaxRequestsPerWindow = 30;
    private const int MaxConversationHistory = 3;

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

    public void RecordRequest(string sessionId)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            data.RequestTimestamps.Add(DateTime.UtcNow);
        }
    }

    public List<ChatMessage> GetConversation(string sessionId)
    {
        var data = Sessions.GetOrAdd(sessionId, _ => new SessionData());
        lock (data.Lock)
        {
            return [..data.Messages];
        }
    }

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

    private sealed class SessionData
    {
        public readonly object Lock = new();
        public List<DateTime> RequestTimestamps { get; } = [];
        public List<ChatMessage> Messages { get; } = [];
    }
}
