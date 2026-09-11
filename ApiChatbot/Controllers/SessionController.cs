using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using ApiChatbot.Models;

namespace ApiChatbot.Controllers;

[ApiController]
[Route("api/v1/session")]
public class SessionController(IConfiguration configuration) : ControllerBase {
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
