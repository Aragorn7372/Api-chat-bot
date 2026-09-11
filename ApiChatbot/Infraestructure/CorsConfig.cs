using Serilog;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración de políticas CORS para la aplicación.
/// En desarrollo permite todos los orígenes; en producción solo los orígenes configurados.
/// </summary>
public static class CorsConfig
{
    /// <summary>
    /// Configura la política CORS según el entorno de ejecución.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <param name="configuration">Configuración para obtener los orígenes permitidos en producción.</param>
    /// <param name="isDevelopment">Si es <c>true</c>, aplica política AllowAll; si es <c>false</c>, usa ProductionPolicy.</param>
    /// <returns>La colección de servicios para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><b>Development:</b> AllowAll — permite cualquier origen, método y header</description></item>
    ///   <item><description><b>Production:</b> ProductionPolicy — solo orígenes en Cors:AllowedOrigins con credenciales</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        Log.Information("Configurando CORS para {Environment}...", isDevelopment ? "DESARROLLO" : "PRODUCCIÓN");

        return services.AddCors(options =>
        {
            if (isDevelopment)
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
                Log.Information("🌐 CORS: AllowAll (desarrollo)");
            }
            else
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                     ?? throw new InvalidOperationException("Cors:AllowedOrigins no configurado");

                options.AddPolicy("ProductionPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
                Log.Information("🌐 CORS: ProductionPolicy con {Count} orígenes", allowedOrigins.Length);
            }
        });
    }
}