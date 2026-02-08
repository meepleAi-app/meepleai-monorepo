using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdatePdfLimitsCommand.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal sealed class UpdatePdfLimitsCommandValidator : AbstractValidator<UpdatePdfLimitsCommand>
{
    public UpdatePdfLimitsCommandValidator()
    {
        RuleFor(x => x.Tier)
            .NotEmpty()
            .WithMessage("Tier is required")
            .Must(BeValidTier)
            .WithMessage("Tier must be one of: free, normal, premium");

        RuleFor(x => x.MaxPerDay)
            .GreaterThan(0)
            .WithMessage("MaxPerDay must be greater than 0");

        RuleFor(x => x.MaxPerWeek)
            .GreaterThanOrEqualTo(x => x.MaxPerDay)
            .WithMessage("MaxPerWeek must be greater than or equal to MaxPerDay");

        RuleFor(x => x.MaxPerGame)
            .GreaterThan(0)
            .WithMessage("MaxPerGame must be greater than 0");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");
    }

    private static bool BeValidTier(string tier)
    {
        if (string.IsNullOrWhiteSpace(tier))
            return false;

        try
        {
            _ = UserTier.Parse(tier);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
