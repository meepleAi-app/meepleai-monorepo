using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for InvalidateGameCacheCommand.
/// </summary>
internal sealed class InvalidateGameCacheCommandValidator : AbstractValidator<InvalidateGameCacheCommand>
{
    public InvalidateGameCacheCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}
