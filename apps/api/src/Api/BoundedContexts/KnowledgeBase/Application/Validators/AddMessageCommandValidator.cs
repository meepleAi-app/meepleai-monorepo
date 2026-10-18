using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for AddMessageCommand.
/// </summary>
internal sealed class AddMessageCommandValidator : AbstractValidator<AddMessageCommand>
{
    public AddMessageCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(2000)
            .WithMessage("Content cannot exceed 2000 characters");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .MaximumLength(50)
            .WithMessage("Role cannot exceed 50 characters")
            .Must(role => string.Equals(role, ChatMessage.UserRole, StringComparison.Ordinal)
                       || string.Equals(role, ChatMessage.AssistantRole, StringComparison.Ordinal))
            .WithMessage("Role must be 'user' or 'assistant'");

        // #4168: la provenienza descrive una risposta, non una domanda
        RuleFor(x => x.ProfileVersionId)
            .Null()
            .When(x => string.Equals(x.Role, ChatMessage.UserRole, StringComparison.Ordinal))
            .WithMessage("ProfileVersionId is only allowed on assistant messages");

        RuleFor(x => x.Language)
            .Null()
            .When(x => string.Equals(x.Role, ChatMessage.UserRole, StringComparison.Ordinal))
            .WithMessage("Language is only allowed on assistant messages");

        RuleFor(x => x.ProfileVersionId)
            .NotEqual(Guid.Empty)
            .When(x => x.ProfileVersionId.HasValue)
            .WithMessage("ProfileVersionId cannot be empty");

        // la pipeline produce un codice ISO 639-1 minuscolo («it-IT» diventa «it»)
        RuleFor(x => x.Language)
            .Matches("^[a-z]{2}$")
            .When(x => x.Language != null)
            .WithMessage("Language must be a two-letter ISO 639-1 code");
    }
}
