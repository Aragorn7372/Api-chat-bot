using ApiChatbot.Background;
using ApiChatbot.Domain;
using ApiChatbot.Validators;
using FluentValidation;
using Serilog;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración centralizada de inyección de dependencias para los servicios del dominio.
/// Registra todos los servicios necesarios: clientes HTTP, validadores, proveedores de IA,
/// almacén de sesiones, analytics, chat service y servicios en segundo plano.
/// </summary>
public static class DomainServicesConfig
{
    /// <summary>
    /// Registra todos los servicios del dominio en el contenedor de dependencias.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La colección de servicios para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// Servicios registrados:
    /// <list type="bullet">
    ///   <item><description>HttpClient "Ollama" (timeout 60s)</description></item>
    ///   <item><description>HttpClient "ContextBuilder" (timeout 30s)</description></item>
    ///   <item><description>UrlContentFetcher (singleton)</description></item>
    ///   <item><description>FluentValidation validators (auto-discovery)</description></item>
    ///   <item><description>OffTopicFilter (singleton)</description></item>
    ///   <item><description>ISessionStore → MemorySessionStore (singleton)</description></item>
    ///   <item><description>IChatProvider → OllamaChatProvider (singleton)</description></item>
    ///   <item><description>IAnalyticsLogger → RedisAnalyticsLogger (singleton)</description></item>
    ///   <item><description>ChatService (singleton)</description></item>
    ///   <item><description>ContextBuilderService (hosted service)</description></item>
    ///   <item><description>OllamaWarmupService (hosted service)</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Information("Registrando servicios del dominio...");

        services.AddHttpClient("Ollama", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("ContextBuilder", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiChatbot/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<UrlContentFetcher>();

        services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();

        var name = configuration["Portfolio:Name"] ?? "";
        services.AddSingleton(new OffTopicFilter(name));
        var contextFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "context.md");
        var systemPromptTemplate = configuration["Portfolio:SystemPrompt"]
            ?? "Eres un asistente para el portafolio profesional.";
        var systemPrompt = systemPromptTemplate.Replace("{Name}", name);

        services.AddSingleton<ISessionStore, MemorySessionStore>();

        services.AddSingleton<IChatProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("Ollama");
            var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var model = configuration["Ollama:Model"] ?? "qwen2.5:1.5b";
            var temperature = configuration.GetValue<double>("Ollama:Temperature", 0.7);
            var logger = sp.GetRequiredService<ILogger<OllamaChatProvider>>();
            return new OllamaChatProvider(client, baseUrl, model, temperature, logger);
        });

        services.AddSingleton<IAnalyticsLogger>(sp =>
        {
            var redisConnection = configuration["Analytics:RedisConnection"];
            var ttlDays = configuration.GetValue<int>("Analytics:TtlDays", 14);
            var logger = sp.GetRequiredService<ILogger<RedisAnalyticsLogger>>();
            return new RedisAnalyticsLogger(redisConnection, ttlDays, logger);
        });

        services.AddSingleton(sp => new ChatService(
            sp.GetRequiredService<IChatProvider>(),
            sp.GetRequiredService<ISessionStore>(),
            sp.GetRequiredService<IAnalyticsLogger>(),
            sp.GetRequiredService<OffTopicFilter>(),
            sp.GetRequiredService<ILogger<ChatService>>(),
            systemPrompt,
            contextFilePath
        ));

        services.AddHostedService<ContextBuilderService>();
        services.AddHostedService<OllamaWarmupService>();

        return services;
    }
}
