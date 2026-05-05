using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for <see cref="QuickCreateAgentCommand"/>. Issue #659 (Phase δ.1).
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
    }
}
