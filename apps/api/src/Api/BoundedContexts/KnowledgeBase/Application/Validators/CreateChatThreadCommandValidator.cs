using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateChatThreadCommand.
/// </summary>
internal sealed class CreateChatThreadCommandValidator : AbstractValidator<CreateChatThreadCommand>
{
    public CreateChatThreadCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title != null)
            .WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.InitialMessage)
            .MaximumLength(2000)
            .When(x => x.InitialMessage != null)
            .WithMessage("InitialMessage cannot exceed 2000 characters");

        // #4998: the rule of the switch (#4885). No intent stays allowed: the thread answers as auto.
        RuleFor(x => x.AgentType)
            .Must(AnswerIntent.IsAcceptedWhenSwitching)
            .When(x => !string.IsNullOrWhiteSpace(x.AgentType))
            .WithMessage($"AgentType must be one of: {string.Join(", ", AnswerIntent.All)}");

        RuleFor(x => x.UserRole)
            .MaximumLength(50)
            .When(x => x.UserRole != null)
            .WithMessage("UserRole cannot exceed 50 characters");
    }
}
