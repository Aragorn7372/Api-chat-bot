using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using ApiChatbot.Models;

namespace ApiChatbot.Controllers;

/// <summary>
/// Controlador para el endpoint de sesiones.
/// Permite crear tokens JWT anónimos para autenticar las peticiones de chat.
/// </summary>
[ApiController]
[Route("api/v1/session")]
public class SessionController(IConfiguration configuration) : ControllerBase {

    /// <summary>
    /// Crea una nueva sesión anónima y devuelve un token JWT.
    /// El token contiene un claim "session_id" con un GUID único y expira según
    /// la configuración (default: 24 horas).
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>200 OK con <see cref="SessionResponse"/> que contiene el token y su expiración</description></item>
    ///   <item><description>500 Internal Server Error si la clave secreta no está configurada</description></item>
    /// </list>
    /// </returns>
    [HttpPost]
    public IActionResult CreateSession()
    {
        var secretKey = configuration["Session:SecretKey"]
            ?? throw new InvalidOperationException("Session:SecretKey no configurado");
        var expiryHours = configuration.GetValue<int>("Session:ExpiryHours", 24);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);
        var sessionId = Guid.NewGuid().ToString("N");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("session_id", sessionId)]),
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new SessionResponse
        {
            Token = tokenString,
            ExpiresAt = tokenDescriptor.Expires!.Value
        });
    }
}
