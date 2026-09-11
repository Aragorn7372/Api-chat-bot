namespace ApiChatbot.Domain;

public class ChatMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public interface IChatProvider
{
    Task<string> GenerateResponseAsync(string systemPrompt, List<ChatMessage> conversation, CancellationToken ct = default);
}
