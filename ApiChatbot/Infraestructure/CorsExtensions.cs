using Serilog;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Extension methods para aplicar la política CORS configurada según el entorno.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Aplica la política CORS correspondiente al entorno actual.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <returns>El constructor de la aplicación para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// Selecciona automáticamente:
    /// <list type="bullet">
    ///   <item><description>"AllowAll" en entorno de desarrollo</description></item>
    ///   <item><description>"ProductionPolicy" en entorno de producción</description></item>
    /// </list>
    /// </remarks>
    public static IApplicationBuilder UseCorsPolicy(this IApplicationBuilder app)
    {
        var env = ((WebApplication)app).Environment;

        var policyName = env.IsDevelopment() ? "AllowAll" : "ProductionPolicy";

        Log.Information("Aplicando política CORS: {PolicyName}", policyName);
        return app.UseCors(policyName);
    }
}