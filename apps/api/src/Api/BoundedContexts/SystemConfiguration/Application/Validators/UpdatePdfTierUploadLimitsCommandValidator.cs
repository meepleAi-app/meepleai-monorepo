using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdatePdfTierUploadLimitsCommand.
/// Ensures limits are within acceptable ranges.
/// Issue #3333: PDF Upload Limits Configuration UI
/// </summary>
internal class UpdatePdfTierUploadLimitsCommandValidator : AbstractValidator<UpdatePdfTierUploadLimitsCommand>
{
    private const int MinDailyLimit = 1;
    private const int MaxDailyLimit = 1000;
    private const int MinWeeklyLimit = 1;
    private const int MaxWeeklyLimit = 5000;

    public UpdatePdfTierUploadLimitsCommandValidator()
    {
        // Free tier limits
        RuleFor(x => x.FreeDailyLimit)
            .InclusiveBetween(MinDailyLimit, MaxDailyLimit)
            .WithMessage($"Free daily limit must be between {MinDailyLimit} and {MaxDailyLimit}");

        RuleFor(x => x.FreeWeeklyLimit)
            .InclusiveBetween(MinWeeklyLimit, MaxWeeklyLimit)
            .WithMessage($"Free weekly limit must be between {MinWeeklyLimit} and {MaxWeeklyLimit}");

        RuleFor(x => x.FreeWeeklyLimit)
            .GreaterThanOrEqualTo(x => x.FreeDailyLimit)
            .WithMessage("Free weekly limit must be greater than or equal to daily limit");

        // Normal tier limits
        RuleFor(x => x.NormalDailyLimit)
            .InclusiveBetween(MinDailyLimit, MaxDailyLimit)
            .WithMessage($"Normal daily limit must be between {MinDailyLimit} and {MaxDailyLimit}");

        RuleFor(x => x.NormalWeeklyLimit)
            .InclusiveBetween(MinWeeklyLimit, MaxWeeklyLimit)
            .WithMessage($"Normal weekly limit must be between {MinWeeklyLimit} and {MaxWeeklyLimit}");

        RuleFor(x => x.NormalWeeklyLimit)
            .GreaterThanOrEqualTo(x => x.NormalDailyLimit)
            .WithMessage("Normal weekly limit must be greater than or equal to daily limit");

        // Premium tier limits
        RuleFor(x => x.PremiumDailyLimit)
            .InclusiveBetween(MinDailyLimit, MaxDailyLimit)
            .WithMessage($"Premium daily limit must be between {MinDailyLimit} and {MaxDailyLimit}");

        RuleFor(x => x.PremiumWeeklyLimit)
            .InclusiveBetween(MinWeeklyLimit, MaxWeeklyLimit)
            .WithMessage($"Premium weekly limit must be between {MinWeeklyLimit} and {MaxWeeklyLimit}");

        RuleFor(x => x.PremiumWeeklyLimit)
            .GreaterThanOrEqualTo(x => x.PremiumDailyLimit)
            .WithMessage("Premium weekly limit must be greater than or equal to daily limit");

        // Tier progression validation (higher tiers should have >= limits)
        RuleFor(x => x.NormalDailyLimit)
            .GreaterThanOrEqualTo(x => x.FreeDailyLimit)
            .WithMessage("Normal tier daily limit should be >= Free tier");

        RuleFor(x => x.PremiumDailyLimit)
            .GreaterThanOrEqualTo(x => x.NormalDailyLimit)
            .WithMessage("Premium tier daily limit should be >= Normal tier");

        RuleFor(x => x.NormalWeeklyLimit)
            .GreaterThanOrEqualTo(x => x.FreeWeeklyLimit)
            .WithMessage("Normal tier weekly limit should be >= Free tier");

        RuleFor(x => x.PremiumWeeklyLimit)
            .GreaterThanOrEqualTo(x => x.NormalWeeklyLimit)
            .WithMessage("Premium tier weekly limit should be >= Normal tier");

        // UpdatedByUserId must be valid
        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");
    }
}
