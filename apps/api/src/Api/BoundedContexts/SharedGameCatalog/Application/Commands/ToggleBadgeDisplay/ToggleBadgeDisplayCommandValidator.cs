using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ToggleBadgeDisplay;

/// <summary>
/// Validator for ToggleBadgeDisplayCommand.
/// Issue #2736: API - Badge Endpoints
/// </summary>
internal sealed class ToggleBadgeDisplayCommandValidator : AbstractValidator<ToggleBadgeDisplayCommand>
{
    public ToggleBadgeDisplayCommandValidator()
    {
        RuleFor(x => x.UserBadgeId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserBadgeId is required");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId is required");
    }
}
