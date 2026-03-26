using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class EnableFeatureForTierCommandValidator : AbstractValidator<EnableFeatureForTierCommand>
{
    public EnableFeatureForTierCommandValidator()
    {
        RuleFor(x => x.FeatureName)
            .NotEmpty()
            .WithMessage("FeatureName is required")
            .MaximumLength(200)
            .WithMessage("FeatureName must not exceed 200 characters");

        RuleFor(x => x.Tier)
            .NotNull()
            .WithMessage("Tier is required");
    }
}
