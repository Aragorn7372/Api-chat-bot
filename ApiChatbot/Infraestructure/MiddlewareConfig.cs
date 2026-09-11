using ApiChatbot.Middleware;
using Serilog;

namespace ApiChatbot.Infraestructure;

public static class MiddlewareConfig
{
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
