using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ApiChatbot.Middleware;

/// <summary>
/// Middleware personalizado para validar tokens JWT de sesión.
/// Intercepta todas las peticiones excepto las del endpoint de creación de sesión.
/// Extrae el token del header "X-Session-Token" y almacena el "session_id" en HttpContext.Items.
/// </summary>
/// <remarks>
/// Flujo de validación:
/// <list type="number">
///   <item><description>Excluye peticiones a /api/v1/session</description></item>
///   <item><description>Lee el header X-Session-Token</description></item>
///   <item><description>Valida la firma HMAC-SHA256 y la expiración del token</description></item>
///   <item><description>Extrae el claim "session_id" y lo almacena en HttpContext.Items["SessionId"]</description></item>
///   <item><description>Retorna 401 si el token está ausente, expirado o es inválido</description></item>
/// </list>
/// </remarks>
public class SessionValidationMiddleware(RequestDelegate next, string secretKey) {
    private readonly SymmetricSecurityKey _securityKey = new(Encoding.UTF8.GetBytes(secretKey));

    /// <summary>
    /// Procesa la petición HTTP actual validando el token de sesión.
    /// </summary>
    /// <param name="context">Contexto HTTP de la petición actual.</param>
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
