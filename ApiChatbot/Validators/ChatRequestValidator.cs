using ApiChatbot.Models;
using FluentValidation;

namespace ApiChatbot.Validators;

public class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje no puede estar vacío")
            .MaximumLength(2000).WithMessage("El mensaje no puede exceder 2000 caracteres");
    }
}
