namespace ApiChatbot.Domain;

public class AnalyticsEntry
{
    public string SessionIdHash { get; init; } = string.Empty;
    public string IpHash { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public long ResponseTimeMs { get; init; }
}

public interface IAnalyticsLogger
{
    Task LogAsync(AnalyticsEntry entry);
}
