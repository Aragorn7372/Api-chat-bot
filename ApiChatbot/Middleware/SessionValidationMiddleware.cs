using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApiChatbot.Middleware;

public class SessionValidationMiddleware(RequestDelegate next, string secretKey) {
    private readonly SymmetricSecurityKey _securityKey = new(Encoding.UTF8.GetBytes(secretKey));

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/session", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers["X-Session-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Se requiere header X-Session-Token" });
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _securityKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var sessionId = principal.Claims.FirstOrDefault(c => c.Type == "session_id")?.Value;
            if (sessionId is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Token inválido: sin session_id" });
                return;
            }

            context.Items["SessionId"] = sessionId;
            await next(context);
        }
        catch (SecurityTokenExpiredException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Token expirado. Solicita uno nuevo en POST /api/v1/session" });
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Token inválido" });
        }
    }
}
