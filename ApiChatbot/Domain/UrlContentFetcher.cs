using System.Text.RegularExpressions;

namespace ApiChatbot.Domain;

/// <summary>
/// Descargador de contenido web con soporte especial para Google Drive.
/// Detecta URLs de Google Drive y maneja el flujo de confirmación de descarga.
/// Para URLs regulares, realiza un HTTP GET simple.
/// </summary>
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

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="UrlContentFetcher"/>.
    /// </summary>
    /// <param name="httpClientFactory">Factory para crear el cliente HTTP "ContextBuilder".</param>
    /// <param name="logger">Logger para registro de eventos.</param>
    public UrlContentFetcher(IHttpClientFactory httpClientFactory, ILogger<UrlContentFetcher> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ContextBuilder");
        _logger = logger;
    }

    /// <summary>
    /// Descarga el contenido de una URL como cadena de texto.
    /// Detecta automáticamente si es una URL de Google Drive y aplica el flujo correspondiente.
    /// </summary>
    /// <param name="url">URL del contenido a descargar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Contenido de texto de la URL.</returns>
    public async Task<string> FetchStringAsync(string url, CancellationToken ct = default)
    {
        var driveId = ExtractGoogleDriveId(url);
        if (driveId is null)
            return await _httpClient.GetStringAsync(url, ct);

        _logger.LogInformation("Detectada URL de Google Drive, fileId: {FileId}", driveId);
        return await DownloadFromGoogleDriveAsync(driveId, ct);
    }

    /// <summary>
    /// Extrae el ID de archivo de una URL de Google Drive.
    /// Soporta formatos: /file/d/{id} y ?id={id}
    /// </summary>
    /// <param name="url">URL de Google Drive.</param>
    /// <returns>ID del archivo o <c>null</c> si no es una URL de Google Drive.</returns>
    private static string? ExtractGoogleDriveId(string url)
    {
        var match = DriveFileRegex.Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        match = DriveIdRegex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Descarga un archivo de Google Drive manejando la página de confirmación de descarga.
    /// </summary>
    /// <param name="fileId">ID del archivo en Google Drive.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Contenido del archivo.</returns>
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

    /// <summary>
    /// Envía una petición GET a Google Drive con User-Agent de navegador.
    /// </summary>
    /// <param name="url">URL a consultar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Contenido de la respuesta.</returns>
    private async Task<string> SendDriveRequestAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Determina si la respuesta contiene una página de confirmación de descarga de Google Drive.
    /// </summary>
    /// <param name="content">Contenido HTML de la respuesta.</param>
    /// <returns><c>true</c> si es una página de confirmación.</returns>
    private static bool ContainsConfirmationPage(string content)
    {
        return content.Contains("confirm=") &&
               (content.Contains("download-form") || content.Contains("uc-download-link"));
    }
}
