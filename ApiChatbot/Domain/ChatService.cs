namespace ApiChatbot.Domain;

public class ChatService(
    IChatProvider chatProvider,
    ISessionStore sessionStore,
    IAnalyticsLogger analyticsLogger,
    OffTopicFilter offTopicFilter,
    ILogger<ChatService> logger,
    string systemPrompt,
    string contextFilePath) {
    public async Task<ChatResult> ProcessMessageAsync(string sessionId, string message, string ipAddress, CancellationToken ct = default)
    {
        if (sessionStore.IsRateLimited(sessionId))
        {
            logger.LogWarning("Rate limit excedido para sesión {SessionId}", sessionId);
            return ChatResult.Failure("Demasiadas solicitudes. Intenta de nuevo en un momento.");
        }

        sessionStore.RecordRequest(sessionId);

        if (offTopicFilter.IsOffTopic(message))
        {
            logger.LogInformation("Mensaje off-topic | Sesión: {SessionId} | Mensaje: {Message}",
                sessionId, message.Length > 100 ? message[..100] + "..." : message);
            return ChatResult.Success(offTopicFilter.RejectionMessage);
        }

        logger.LogInformation("Procesando mensaje | Sesión: {SessionId} | IP: {IpAddress} | Mensaje: {Message}",
            sessionId, ipAddress, message.Length > 100 ? message[..100] + "..." : message);

        var context = await LoadContextAsync();
        var fullPrompt = string.IsNullOrWhiteSpace(context)
            ? systemPrompt
            : $"{systemPrompt}\n\nContexto sobre mí:\n---\n{context}\n---";

        sessionStore.AddMessage(sessionId, new ChatMessage { Role = "user", Content = message });
        var conversation = sessionStore.GetConversation(sessionId);

        var estimatedTokens = fullPrompt.Length / 4 + conversation.Sum(m => m.Content.Length) / 4;
        var trimmed = false;
        while (estimatedTokens > 3000 && conversation.Count >= 2)
        {
            conversation.RemoveRange(0, 2);
            estimatedTokens = fullPrompt.Length / 4 + conversation.Sum(m => m.Content.Length) / 4;
            trimmed = true;
        }
        if (trimmed)
            logger.LogInformation("Contexto truncado para sesión {SessionId}: {Count} mensajes restantes", sessionId, conversation.Count);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string response;
        try
        {
            response = await chatProvider.GenerateResponseAsync(fullPrompt, conversation, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al llamar al proveedor LLM para sesión {SessionId}", sessionId);
            return ChatResult.Failure("Error al generar la respuesta. Intenta de nuevo.");
        }
        sw.Stop();

        logger.LogInformation("Respuesta generada | Sesión: {SessionId} | Tiempo: {ElapsedMs}ms | Tamaño: {Size} caracteres",
            sessionId, sw.ElapsedMilliseconds, response.Length);

        sessionStore.AddMessage(sessionId, new ChatMessage { Role = "assistant", Content = response });

        _ = Task.Run(() => LogAnalyticsAsync(sessionId, ipAddress, message, response, sw.ElapsedMilliseconds), CancellationToken.None);

        return ChatResult.Success(response);
    }

    private async Task LogAnalyticsAsync(string sessionId, string ipAddress, string message, string response, long elapsedMs)
    {
        try
        {
            await analyticsLogger.LogAsync(new AnalyticsEntry
            {
                SessionIdHash = HashHelper.Sha256(sessionId),
                IpHash = HashHelper.Sha256(ipAddress),
                Question = message,
                Response = response,
                Timestamp = DateTime.UtcNow,
                ResponseTimeMs = elapsedMs
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error al loguear analytics");
        }
    }

    private async Task<string> LoadContextAsync()
    {
        try
        {
            if (File.Exists(contextFilePath))
            {
                return await File.ReadAllTextAsync(contextFilePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error al leer archivo de contexto");
        }
        return string.Empty;
    }
}

public class ChatResult
{
    public bool IsSuccess { get; private init; }
    public string? Reply { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ChatResult Success(string reply) => new() { IsSuccess = true, Reply = reply };
    public static ChatResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
