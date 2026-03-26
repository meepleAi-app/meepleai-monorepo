using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.RemoveUserRateLimitOverride;

internal sealed class RemoveUserRateLimitOverrideCommandValidator : AbstractValidator<RemoveUserRateLimitOverrideCommand>
{
    public RemoveUserRateLimitOverrideCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
