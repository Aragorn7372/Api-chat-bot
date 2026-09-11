using Serilog;

namespace ApiChatbot.Infraestructure;

public static class SwaggerConfig
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

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
