namespace ApiChatbot.Domain;

public interface ISessionStore
{
    bool IsRateLimited(string sessionId);
    void RecordRequest(string sessionId);
    List<ChatMessage> GetConversation(string sessionId);
    void AddMessage(string sessionId, ChatMessage message);
}
