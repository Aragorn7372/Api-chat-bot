using ApiChatbot.Infraestructure;
using Serilog;

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
