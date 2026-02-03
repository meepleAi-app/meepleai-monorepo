using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdateSessionLimitsCommand.
/// Enforces session limit constraints: minimum 1 (or -1 for unlimited), maximum 100 sessions per tier.
/// Issue #3070: Session limits backend implementation.
/// </summary>
internal sealed class UpdateSessionLimitsCommandValidator : AbstractValidator<UpdateSessionLimitsCommand>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 100;
    private const int UnlimitedValue = -1;

    public UpdateSessionLimitsCommandValidator()
    {
        RuleFor(x => x.FreeTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"Free tier limit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.NormalTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"Normal tier limit must be between {MinLimit} and {MaxLimit}");

        // Premium tier can be -1 (unlimited) or between MinLimit and MaxLimit
        RuleFor(x => x.PremiumTierLimit)
            .Must(v => v == UnlimitedValue || (v >= MinLimit && v <= MaxLimit))
            .WithMessage($"Premium tier limit must be {UnlimitedValue} (unlimited) or between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");

        // Business rule: Normal tier should have at least as many sessions as Free tier
        RuleFor(x => x.NormalTierLimit)
            .GreaterThanOrEqualTo(x => x.FreeTierLimit)
            .WithMessage("Normal tier limit must be greater than or equal to Free tier limit");

        // Business rule: Premium tier should be unlimited or have at least as many sessions as Normal tier
        RuleFor(x => x)
            .Must(x => x.PremiumTierLimit == UnlimitedValue || x.PremiumTierLimit >= x.NormalTierLimit)
            .WithMessage("Premium tier limit must be unlimited (-1) or greater than or equal to Normal tier limit");
    }
}
