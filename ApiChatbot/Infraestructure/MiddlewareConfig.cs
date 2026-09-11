using ApiChatbot.Middleware;
using Serilog;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración del pipeline de middleware de ASP.NET Core.
/// Define el orden de ejecución de los middleware: Serilog, HTTPS, CORS,
/// validación JWT de sesión y rate limiting.
/// </summary>
public static class MiddlewareConfig
{
    /// <summary>
    /// Configura y ordena todos los middleware de la aplicación.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="env">Entorno de ejecución (Development/Production).</param>
    /// <returns>El constructor de la aplicación para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// Orden de middleware:
    /// <list type="number">
    ///   <item><description>Serilog request logging</description></item>
    ///   <item><description>HTTPS redirection (solo en Development)</description></item>
    ///   <item><description>CORS</description></item>
    ///   <item><description>Session validation middleware (JWT)</description></item>
    ///   <item><description>Rate limiting</description></item>
    /// </list>
    /// </remarks>
    public static IApplicationBuilder UseAppMiddleware(this IApplicationBuilder app, IConfiguration configuration, IWebHostEnvironment env)
    {
        app.UseSerilogRequestLogging();

        if (env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCorsPolicy();

        var secretKey = configuration["Session:SecretKey"]
            ?? throw new InvalidOperationException("Session:SecretKey es requerido");
        app.UseMiddleware<SessionValidationMiddleware>(secretKey);

        app.UseRateLimiting();

        return app;
    }
}
