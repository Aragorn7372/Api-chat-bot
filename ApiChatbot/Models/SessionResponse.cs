namespace ApiChatbot.Models;

public class SessionResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
