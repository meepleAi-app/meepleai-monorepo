using Api.BoundedContexts.KnowledgeBase.Application.Commands;
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

        RuleFor(x => x.AgentType)
            .MaximumLength(200)
            .When(x => x.AgentType != null)
            .WithMessage("AgentType cannot exceed 200 characters");

        RuleFor(x => x.UserRole)
            .MaximumLength(50)
            .When(x => x.UserRole != null)
            .WithMessage("UserRole cannot exceed 50 characters");
    }
}
