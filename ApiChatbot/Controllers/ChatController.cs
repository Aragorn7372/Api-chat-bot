using ApiChatbot.Domain;
using ApiChatbot.Models;
using ApiChatbot.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ApiChatbot.Controllers;

[ApiController]
[Route("api/v1/chat")]
public class ChatController(ChatService chatService, ChatRequestValidator validator, ILogger<ChatController> logger) : ControllerBase {
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Validación fallida: {Errors}", validationResult.Errors);
            return BadRequest(validationResult.Errors);
        }

        var sessionId = HttpContext.Items["SessionId"] as string;
        if (sessionId is null)
        {
            logger.LogWarning("Request sin sessionId válido");
            return Unauthorized(new { error = "Sesión no válida" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logger.LogInformation("Chat Request | Sesión: {SessionId} | IP: {IpAddress}", sessionId, ipAddress);

        var result = await chatService.ProcessMessageAsync(sessionId, request.Message, ipAddress, ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Chat falló | Sesión: {SessionId} | Error: {Error}", sessionId, result.ErrorMessage);
            return result.ErrorMessage switch
            {
                var e when e?.Contains("Demasiadas") == true => StatusCode(429, new { error = result.ErrorMessage }),
                _ => StatusCode(500, new { error = result.ErrorMessage })
            };
        }

        return Ok(new ChatResponse { Reply = result.Reply! });
    }
}
