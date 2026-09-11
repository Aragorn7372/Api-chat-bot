using System.Text.Json;
using ApiChatbot.Domain;

namespace ApiChatbot.Background;

/// <summary>
/// Servicio en segundo plano que construye y actualiza la base de conocimiento del chatbot.
/// Descarga archivos markdown desde Google Drive, consulta la API de GitHub para obtener
/// actividad reciente, repositorios y repositorios destacados, y genera el archivo
/// <c>Data/context.md</c> que el <see cref="ChatService"/> utiliza como contexto del LLM.
/// </summary>
/// <remarks>
/// Ejecución:
/// <list type="number">
///   <item><description>Se ejecuta inmediatamente al iniciar la aplicación</description></item>
///   <item><description>Se re-ejecuta cada 24 horas automáticamente</description></item>
/// </list>
/// Fuentes de datos:
/// <list type="bullet">
///   <item><description>Archivos markdown de Google Drive (About, Skills, Projects)</description></item>
///   <item><description>API de GitHub: eventos, repositorios propios y repositorios destacados</description></item>
/// </list>
/// </remarks>
public class ContextBuilderService : BackgroundService
{
    private readonly ILogger<ContextBuilderService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly UrlContentFetcher _urlFetcher;
    private readonly string _dataDir;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ContextBuilderService"/>.
    /// </summary>
    /// <param name="logger">Logger para registro de eventos.</param>
    /// <param name="configuration">Configuración de la aplicación para URLs y usuario de GitHub.</param>
    /// <param name="httpClientFactory">Factory para crear el cliente HTTP "ContextBuilder".</param>
    /// <param name="urlFetcher">Descargador de contenido con soporte Google Drive.</param>
    public ContextBuilderService(
        ILogger<ContextBuilderService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        UrlContentFetcher urlFetcher)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("ContextBuilder");
        _urlFetcher = urlFetcher;
        _dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
    }

    /// <summary>
    /// Ejecuta el servicio de construcción de contexto.
    /// Construye el contexto inicial y luego se re-ejecuta cada 24 horas.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelación para detener el servicio.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContextBuilderService iniciado");

        await BuildContextAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await BuildContextAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Construye el archivo context.md descargando contenido de las URLs configuradas
    /// y consultando la API de GitHub.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    private async Task BuildContextAsync(CancellationToken ct)
    {
        _logger.LogInformation("Construyendo context.md...");
        Directory.CreateDirectory(_dataDir);

        var parts = new List<string>();

        var urls = new Dictionary<string, string>
        {
            ["Acerca de mí"] = _configuration["Portfolio:ContextUrls:About"] ?? "",
            ["Habilidades"] = _configuration["Portfolio:ContextUrls:Skills"] ?? "",
            ["Proyectos"] = _configuration["Portfolio:ContextUrls:Projects"] ?? ""
        };

        foreach (var (section, url) in urls)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            try
            {
                _logger.LogInformation("Descargando {Section} desde {Url}", section, url);
                var content = await _urlFetcher.FetchStringAsync(url, ct);
                parts.Add($"# {section}\n{content.Trim()}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al descargar {Section}", section);
            }
        }

        var githubUsername = _configuration["Portfolio:GithubUsername"];
        if (!string.IsNullOrWhiteSpace(githubUsername))
        {
            var activity = await GetGitHubActivityAsync(githubUsername, ct);
            if (!string.IsNullOrWhiteSpace(activity))
                parts.Add(activity);
        }

        var result = string.Join("\n\n---\n\n", parts);
        var filePath = Path.Combine(_dataDir, "context.md");
        await File.WriteAllTextAsync(filePath, result, ct);

        _logger.LogInformation("context.md actualizado ({Size} caracteres)", result.Length);
    }

    /// <summary>
    /// Obtiene la actividad reciente de GitHub: eventos, repositorios propios y destacados.
    /// </summary>
    /// <param name="username">Usuario de GitHub.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Texto formateado con la actividad de GitHub o cadena vacía si hay error.</returns>
    private async Task<string> GetGitHubActivityAsync(string username, CancellationToken ct)
    {
        var lines = new List<string>();

        try
        {
            var eventsJson = await _httpClient.GetStringAsync(
                $"https://api.github.com/users/{username}/events?per_page=30", ct);
            var events = JsonSerializer.Deserialize<List<GitHubEvent>>(eventsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (events?.Count > 0)
            {
                lines.Add("# Actividad Reciente en GitHub\n");
                foreach (var evt in events.Take(20))
                {
                    var formatted = FormatEvent(evt);
                    if (formatted is not null)
                        lines.Add($"- {formatted}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener eventos de GitHub");
        }

        try
        {
            var reposJson = await _httpClient.GetStringAsync(
                $"https://api.github.com/users/{username}/repos?sort=updated&per_page=10&type=owner", ct);
            var repos = JsonSerializer.Deserialize<List<GitHubRepo>>(reposJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (repos?.Count > 0)
            {
                lines.Add("\n# Repositorios\n");
                foreach (var repo in repos)
                {
                    var desc = string.IsNullOrWhiteSpace(repo.Description) ? "Sin descripción" : repo.Description;
                    lines.Add($"- [{repo.Name}]({repo.HtmlUrl}) — {desc}  ⭐{repo.StargazersCount}  🍴{repo.ForksCount}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener repos de GitHub");
        }

        try
        {
            var starredJson = await _httpClient.GetStringAsync(
                $"https://api.github.com/users/{username}/starred?per_page=10&sort=created", ct);
            var starred = JsonSerializer.Deserialize<List<GitHubRepo>>(starredJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (starred?.Count > 0)
            {
                lines.Add("\n# Repositorios que he destacado (⭐)\n");
                foreach (var repo in starred)
                {
                    var desc = string.IsNullOrWhiteSpace(repo.Description) ? "Sin descripción" : repo.Description;
                    lines.Add($"- [{repo.FullName}]({repo.HtmlUrl}) — {desc}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener starred repos");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Formatea un evento de GitHub con un emoji descriptivo y tiempo relativo.
    /// </summary>
    /// <param name="evt">Evento de GitHub a formatear.</param>
    /// <returns>Cadena formateada o <c>null</c> si el tipo de evento no es soportado.</returns>
    private static string? FormatEvent(GitHubEvent evt)
    {
        var repo = evt.Repo?.Name ?? "desconocido";
        var ago = TimeAgo(evt.CreatedAt);

        return evt.Type switch
        {
            "PushEvent" => $"🚀 Push a {repo} — {ago}",
            "WatchEvent" => $"⭐ Dio estrella a {repo} — {ago}",
            "ForkEvent" => $"🍴 Hizo fork de {repo} — {ago}",
            "CreateEvent" => $"📦 Creó {evt.Payload?.RefType ?? "elemento"} en {repo} — {ago}",
            "DeleteEvent" => $"🗑️ Eliminó {evt.Payload?.RefType ?? "elemento"} en {repo} — {ago}",
            "IssuesEvent" => $"🐛 {(evt.Payload?.Action ?? "actualizó")} issue en {repo} — {ago}",
            "IssueCommentEvent" => $"💬 Comentó en issue de {repo} — {ago}",
            "PullRequestEvent" => $"🔀 {(evt.Payload?.Action ?? "actualizó")} PR en {repo} — {ago}",
            "PullRequestReviewEvent" => $"👀 Revisó PR en {repo} — {ago}",
            "ReleaseEvent" => $"🏷️ Publicó release en {repo} — {ago}",
            _ => null
        };
    }

    /// <summary>
    /// Calcula el tiempo relativo transcurrido desde una fecha.
    /// </summary>
    /// <param name="dt">Fecha a comparar.</param>
    /// <returns>Cadena con el tiempo relativo (ej: "hace 5 min", "hace 2 días").</returns>
    private static string TimeAgo(DateTime? dt)
    {
        if (dt is null) return "";
        var diff = DateTime.UtcNow - dt.Value;
        return diff.TotalMinutes < 2 ? "ahora mismo" :
               diff.TotalHours < 1 ? $"hace {(int)diff.TotalMinutes} min" :
               diff.TotalDays < 1 ? $"hace {(int)diff.TotalHours} h" :
               diff.TotalDays < 30 ? $"hace {(int)diff.TotalDays} días" :
               $"hace {(int)(diff.TotalDays / 30)} meses";
    }
}

/// <summary>
/// DTO que representa un evento de la API de GitHub.
/// </summary>
public record GitHubEvent
{
    /// <summary>Tipo de evento (PushEvent, WatchEvent, ForkEvent, etc.).</summary>
    public string? Type { get; init; }

    /// <summary>Fecha y hora de creación del evento.</summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>Referencia al repositorio asociado al evento.</summary>
    public GitHubRepoRef? Repo { get; init; }

    /// <summary>Datos adicionales del evento (acción, tipo de referencia, etc.).</summary>
    public GitHubPayload? Payload { get; init; }
}

/// <summary>
/// DTO que representa una referencia a un repositorio de GitHub.
/// </summary>
public record GitHubRepoRef
{
    /// <summary>Nombre del repositorio.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// DTO que contiene datos adicionales de un evento de GitHub.
/// </summary>
public record GitHubPayload
{
    /// <summary>Acción realizada (ej: "created", "closed").</summary>
    public string? Action { get; init; }

    /// <summary>Tipo de referencia (ej: "branch", "tag").</summary>
    public string? RefType { get; init; }
}

/// <summary>
/// DTO que representa un repositorio de GitHub.
/// </summary>
public record GitHubRepo
{
    /// <summary>Nombre corto del repositorio.</summary>
    public string? Name { get; init; }

    /// <summary>Nombre completo (usuario/repo).</summary>
    public string? FullName { get; init; }

    /// <summary>URL HTML del repositorio.</summary>
    public string? HtmlUrl { get; init; }

    /// <summary>Descripción del repositorio.</summary>
    public string? Description { get; init; }

    /// <summary>Número de estrellas.</summary>
    public int StargazersCount { get; init; }

    /// <summary>Número de forks.</summary>
    public int ForksCount { get; init; }
}
