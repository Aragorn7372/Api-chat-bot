using System.Text.RegularExpressions;

namespace ApiChatbot.Domain;

public class UrlContentFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UrlContentFetcher> _logger;

    private static readonly Regex DriveFileRegex = new(
        @"(?:file/d/)([^/\?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DriveIdRegex = new(
        @"(?:[\?&]id=)([^&]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConfirmRegex = new(
        @"confirm=([^&\""]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UrlContentFetcher(IHttpClientFactory httpClientFactory, ILogger<UrlContentFetcher> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ContextBuilder");
        _logger = logger;
    }

    public async Task<string> FetchStringAsync(string url, CancellationToken ct = default)
    {
        var driveId = ExtractGoogleDriveId(url);
        if (driveId is null)
            return await _httpClient.GetStringAsync(url, ct);

        _logger.LogInformation("Detectada URL de Google Drive, fileId: {FileId}", driveId);
        return await DownloadFromGoogleDriveAsync(driveId, ct);
    }

    private static string? ExtractGoogleDriveId(string url)
    {
        var match = DriveFileRegex.Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        match = DriveIdRegex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<string> DownloadFromGoogleDriveAsync(string fileId, CancellationToken ct)
    {
        var baseUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

        var content = await SendDriveRequestAsync(baseUrl, ct);

        if (ContainsConfirmationPage(content))
        {
            var confirmMatch = ConfirmRegex.Match(content);
            if (confirmMatch.Success)
            {
                var confirmUrl = $"{baseUrl}&confirm=t";
                _logger.LogInformation("Interstitial detectado, reintentando con confirm=t");
                content = await SendDriveRequestAsync(confirmUrl, ct);
            }
        }

        return content;
    }

    private async Task<string> SendDriveRequestAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static bool ContainsConfirmationPage(string content)
    {
        return content.Contains("confirm=") &&
               (content.Contains("download-form") || content.Contains("uc-download-link"));
    }
}
