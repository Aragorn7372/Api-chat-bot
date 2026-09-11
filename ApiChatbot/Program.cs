using ApiChatbot.Infraestructure;
using Serilog;

/// <summary>
/// Entry point de la aplicación ApiChatbot.
/// Configura el host web, registra servicios, middleware y ejecuta la API.
/// </summary>
/// <remarks>
/// Flujo de inicialización:
/// <list type="number">
///   <item><description>Carga variables de entorno desde .env (si existe)</description></item>
///   <item><description>Configura Serilog bootstrap logger</description></item>
///   <item><description>Registra servicios: controllers, Swagger, CORS, rate limiting, dominio</description></item>
///   <item><description>Configura middleware: Serilog, HTTPS, CORS, JWT, rate limiting</description></item>
///   <item><description>Mapea controllers y ejecuta la aplicación</description></item>
/// </list>
/// En caso de error fatal, se registra con Serilog antes de terminar.
/// </remarks>
Log.Logger = SerilogConfig.Configure().CreateBootstrapLogger();

try
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
    }

    var builder = WebApplication.CreateBuilder(args);
    var services = builder.Services;
    var configuration = builder.Configuration;
    var env = builder.Environment;

    builder.Host.UseSerilog();

    configuration.AddEnvironmentVariables();

    services.AddControllers();
    services.AddSwaggerDocumentation();
    services.AddCorsPolicy(configuration, env.IsDevelopment());
    services.AddRateLimitingPolicy();
    services.AddDomainServices(configuration);

    var app = builder.Build();

    app.UseSwaggerDocumentation(env);
    app.UseAppMiddleware(configuration, env);
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
