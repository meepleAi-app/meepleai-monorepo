using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for AddChatSessionMessageCommand.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class AddChatSessionMessageCommandValidator : AbstractValidator<AddChatSessionMessageCommand>
{
    private static readonly string[] ValidRoles = { "user", "assistant", "system" };

    public AddChatSessionMessageCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .Must(role => ValidRoles.Contains(role, StringComparer.Ordinal))
            .WithMessage("Role must be 'user', 'assistant', or 'system'");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(50000)
            .WithMessage("Content cannot exceed 50,000 characters");
    }
}
