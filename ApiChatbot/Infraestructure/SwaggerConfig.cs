using Serilog;

namespace ApiChatbot.Infraestructure;

/// <summary>
/// Configuración de documentación OpenAPI/Swagger.
/// Habilita la generación de documentación de la API en modo desarrollo.
/// </summary>
public static class SwaggerConfig
{
    /// <summary>
    /// Registra el servicio OpenAPI en el contenedor de dependencias.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <returns>La colección de servicios para encadenamiento de llamadas.</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    /// <summary>
    /// Mapea el endpoint de OpenAPI solo en entorno de desarrollo.
    /// </summary>
    /// <param name="app">Constructor de la aplicación.</param>
    /// <param name="env">Entorno de ejecución.</param>
    /// <returns>El constructor de la aplicación para encadenamiento de llamadas.</returns>
    /// <remarks>
    /// En desarrollo, el endpoint está disponible en: /openapi/v1.json
    /// En producción, no se expone por seguridad.
    /// </remarks>
    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            var webApp = (WebApplication)app;
            webApp.MapOpenApi();
        }
        return app;
    }
}
