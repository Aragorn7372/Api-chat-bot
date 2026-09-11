using System.Text.Json;
using ApiChatbot.Domain;

namespace ApiChatbot.Background;

public class ContextBuilderService : BackgroundService
{
    private readonly ILogger<ContextBuilderService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly UrlContentFetcher _urlFetcher;
    private readonly string _dataDir;

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

public record GitHubEvent
{
    public string? Type { get; init; }
    public DateTime? CreatedAt { get; init; }
    public GitHubRepoRef? Repo { get; init; }
    public GitHubPayload? Payload { get; init; }
}

public record GitHubRepoRef
{
    public string? Name { get; init; }
}

public record GitHubPayload
{
    public string? Action { get; init; }
    public string? RefType { get; init; }
}

public record GitHubRepo
{
    public string? Name { get; init; }
    public string? FullName { get; init; }
    public string? HtmlUrl { get; init; }
    public string? Description { get; init; }
    public int StargazersCount { get; init; }
    public int ForksCount { get; init; }
}
