using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for QuickPublishSharedGameCommand.
/// Issue #250: Quick-publish endpoint for admin shared games
/// </summary>
internal sealed class QuickPublishSharedGameCommandValidator : AbstractValidator<QuickPublishSharedGameCommand>
{
    public QuickPublishSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.PublishedBy)
            .NotEmpty().WithMessage("PublishedBy is required");
    }
}
