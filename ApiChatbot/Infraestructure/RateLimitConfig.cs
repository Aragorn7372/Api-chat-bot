using AspNetCoreRateLimit;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración de rate limiting para proteger la API contra DDoS, fuerza bruta y abuso.
/// Utiliza AspNetCoreRateLimit con almacenamiento en memoria.
/// </summary>
public static class RateLimitConfig
{
    /// <summary>
    /// Configura el servicio de rate limiting con reglas por endpoint.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <returns>La colección de servicios para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// Reglas configuradas:
    /// <list type="bullet">
    ///   <item><description><b>General:</b> 100 peticiones por 15 segundos</description></item>
    ///   <item><description><b>Auth:</b> 10 peticiones por minuto (protección contra fuerza bruta)</description></item>
    ///   <item><description><b>POST:</b> 20 peticiones por minuto (endpoints de escritura)</description></item>
    ///   <item><description><b>GraphQL:</b> 200 peticiones por minuto (más permisivo para queries)</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddRateLimitingPolicy(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.Configure<RateLimitOptions>(options =>
        {
            options.EnableEndpointRateLimiting = true;
            options.HttpStatusCode = 429;
            options.QuotaExceededMessage = "Demasiadas solicitudes. Por favor, intente más tarde.";
            
            options.GeneralRules = new List<RateLimitRule>
            {
                new RateLimitRule
                {
                    Endpoint = "*",
                    Limit = 100,
                    Period = "15s"
                },
                new RateLimitRule
                {
                    Endpoint = "*/api/v1/auth/*",
                    Limit = 10,
                    Period = "1m"
                },
                new RateLimitRule
                {
                    Endpoint = "POST:*",
                    Limit = 20,
                    Period = "1m"
                },
                new RateLimitRule
                {
                    Endpoint = "POST:/graphql",
                    Limit = 200,
                    Period = "1m"
                }
            };
        });

        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        
        return services;
    }

    /// <summary>
    /// Aplica el middleware de rate limiting por IP.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <returns>El constructor de la aplicación para encadenamiento de llamadas.</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        app.UseIpRateLimiting();
        return app;
    }
}