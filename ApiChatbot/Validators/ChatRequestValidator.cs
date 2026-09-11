using ApiChatbot.Models;
using FluentValidation;

namespace ApiChatbot.Validators;

/// <summary>
/// Validador FluentValidation para <see cref="ChatRequest"/>.
/// Aplica reglas de validación al mensaje del usuario antes de procesarlo.
/// </summary>
public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    /// <summary>
    /// Inicializa las reglas de validación:
    /// <list type="bullet">
    ///   <item><description>Message: no puede estar vacío</description></item>
    ///   <item><description>Message: máximo 2000 caracteres</description></item>
    /// </list>
    /// Los mensajes de error están en español.
    /// </summary>
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje no puede estar vacío")
            .MaximumLength(2000).WithMessage("El mensaje no puede exceder 2000 caracteres");
    }
}
