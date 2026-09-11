namespace ApiChatbot.Domain;

/// <summary>
/// Servicio principal de orquestación del chat.
/// Gestiona el flujo completo: rate limiting, filtro off-topic, construcción de prompt,
/// llamada al proveedor de IA, persistencia de historial y registro de analytics.
/// </summary>
public class ChatService(
    IChatProvider chatProvider,
    ISessionStore sessionStore,
    IAnalyticsLogger analyticsLogger,
    OffTopicFilter offTopicFilter,
    ILogger<ChatService> logger,
    string systemPrompt,
    string contextFilePath) {

    /// <summary>
    /// Procesa un mensaje del usuario y genera una respuesta del chatbot.
    /// </summary>
    /// <param name="sessionId">Identificador de la sesión del usuario.</param>
    /// <param name="message">Mensaje enviado por el usuario.</param>
    /// <param name="ipAddress">Dirección IP del cliente (para analytics).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see cref="ChatResult.Success"/> con la respuesta si el procesamiento fue exitoso,
    /// o <see cref="ChatResult.Failure"/> con el mensaje de error correspondiente.
    /// </returns>
    /// <remarks>
    /// Flujo de procesamiento:
    /// <list type="number">
    ///   <item><description>Verifica rate limiting de la sesión</description></item>
    ///   <item><description>Registra la petición para control de tasa</description></item>
    ///   <item><description>Aplica filtro de mensajes off-topic</description></item>
    ///   <item><description>Carga el contexto desde el archivo context.md</description></item>
    ///   <item><description>Construye el prompt completo con contexto</description></item>
    ///   <item><description>Recorta el historial si excede ~3000 tokens estimados</description></item>
    ///   <item><description>Llama al proveedor de IA para generar la respuesta</description></item>
    ///   <item><description>Persiste el historial en el almacén de sesiones</description></item>
    ///   <item><description>Registra analytics de forma asíncrona (fire-and-forget)</description></item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Registra los datos de analytics de forma asíncrona y aislada.
    /// En caso de error, se registra un warning pero no interrumpe el flujo principal.
    /// </summary>
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

    /// <summary>
    /// Carga el archivo de contexto (context.md) desde disco.
    /// </summary>
    /// <returns>El contenido del archivo o una cadena vacía si no existe o hay error.</returns>
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

/// <summary>
/// Resultado del procesamiento de un mensaje de chat.
/// Implementa el patrón Result para evitar excepciones en el flujo de control.
/// </summary>
public class ChatResult
{
    /// <summary>
    /// Indica si el procesamiento fue exitoso.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Respuesta generada por el chatbot. Solo tiene valor cuando <see cref="IsSuccess"/> es <c>true</c>.
    /// </summary>
    public string? Reply { get; private init; }

    /// <summary>
    /// Mensaje de error. Solo tiene valor cuando <see cref="IsSuccess"/> es <c>false</c>.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Crea un resultado exitoso con la respuesta del chatbot.
    /// </summary>
    /// <param name="reply">Texto de la respuesta generada.</param>
    /// <returns>Instancia de <see cref="ChatResult"/> con estado de éxito.</returns>
    public static ChatResult Success(string reply) => new() { IsSuccess = true, Reply = reply };

    /// <summary>
    /// Crea un resultado fallido con el mensaje de error.
    /// </summary>
    /// <param name="error">Descripción del error ocurrido.</param>
    /// <returns>Instancia de <see cref="ChatResult"/> con estado de error.</returns>
    public static ChatResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
