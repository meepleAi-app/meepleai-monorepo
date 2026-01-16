using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdateGameLibraryLimitsCommand.
/// Enforces tier limit constraints: minimum 1, maximum 1000 games per tier.
/// Issue #2444: Admin UI - Configure Game Library Tier Limits
/// </summary>
internal sealed class UpdateGameLibraryLimitsCommandValidator : AbstractValidator<UpdateGameLibraryLimitsCommand>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;

    public UpdateGameLibraryLimitsCommandValidator()
    {
        RuleFor(x => x.FreeTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"Free tier limit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.NormalTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"Normal tier limit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.PremiumTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"Premium tier limit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");

        // Business rule: Normal tier should have at least as many games as Free tier
        RuleFor(x => x.NormalTierLimit)
            .GreaterThanOrEqualTo(x => x.FreeTierLimit)
            .WithMessage("Normal tier limit must be greater than or equal to Free tier limit");

        // Business rule: Premium tier should have at least as many games as Normal tier
        RuleFor(x => x.PremiumTierLimit)
            .GreaterThanOrEqualTo(x => x.NormalTierLimit)
            .WithMessage("Premium tier limit must be greater than or equal to Normal tier limit");
    }
}
