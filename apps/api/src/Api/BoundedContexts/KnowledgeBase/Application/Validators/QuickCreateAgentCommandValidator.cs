using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for QuickCreateAgentCommand.
/// </summary>
internal class QuickCreateAgentCommandValidator : AbstractValidator<QuickCreateAgentCommand>
{
    public QuickCreateAgentCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.SharedGameId)
            .Must(id => id == null || id != Guid.Empty)
            .When(x => x.SharedGameId.HasValue)
            .WithMessage("SharedGameId cannot be empty when provided");
    }
}
