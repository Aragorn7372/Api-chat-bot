using ApiChatbot.Domain;
using ApiChatbot.Models;
using ApiChatbot.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ApiChatbot.Controllers;

/// <summary>
/// Controlador para el endpoint de chat.
/// Recibe mensajes del usuario, valida la entrada y delega al <see cref="ChatService"/>
/// para generar respuestas del chatbot.
/// </summary>
[ApiController]
[Route("api/v1/chat")]
public class ChatController(ChatService chatService, ChatRequestValidator validator, ILogger<ChatController> logger) : ControllerBase {

    /// <summary>
    /// Envía un mensaje al chatbot y recibe una respuesta generada por IA.
    /// </summary>
    /// <param name="request">DTO con el mensaje del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>200 OK con <see cref="ChatResponse"/> si el procesamiento fue exitoso</description></item>
    ///   <item><description>400 Bad Request si la validación falla</description></item>
    ///   <item><description>401 Unauthorized si la sesión no es válida</description></item>
    ///   <item><description>429 Too Many Requests si se excede el rate limit</description></item>
    ///   <item><description>500 Internal Server Error si falla el proveedor de IA</description></item>
    /// </list>
    /// </returns>
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
