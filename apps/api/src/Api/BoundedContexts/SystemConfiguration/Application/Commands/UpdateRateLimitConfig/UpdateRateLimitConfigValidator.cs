using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateRateLimitConfig;

/// <summary>
/// Validator for UpdateRateLimitConfigCommand.
/// Ensures valid rate limit values and prevents rate limiting of Admin tier.
/// </summary>
internal sealed class UpdateRateLimitConfigValidator : AbstractValidator<UpdateRateLimitConfigCommand>
{
    private const int MinPendingRequests = 1;
    private const int MaxPendingRequests = 100;
    private const int MinRequestsPerMonth = 1;
    private const int MaxRequestsPerMonth = 1000;
    private static readonly TimeSpan MinCooldown = TimeSpan.Zero;
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromDays(30);

    public UpdateRateLimitConfigValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");

        RuleFor(x => x.Tier)
            .IsInEnum()
            .WithMessage("Invalid user tier")
            .NotEqual(UserTier.Admin)
            .WithMessage("Admin tier cannot be rate limited");

        RuleFor(x => x.MaxPendingRequests)
            .InclusiveBetween(MinPendingRequests, MaxPendingRequests)
            .WithMessage($"Max pending requests must be between {MinPendingRequests} and {MaxPendingRequests}");

        RuleFor(x => x.MaxRequestsPerMonth)
            .InclusiveBetween(MinRequestsPerMonth, MaxRequestsPerMonth)
            .WithMessage($"Max requests per month must be between {MinRequestsPerMonth} and {MaxRequestsPerMonth}");

        RuleFor(x => x.CooldownAfterRejection)
            .GreaterThanOrEqualTo(MinCooldown)
            .WithMessage($"Cooldown cannot be negative")
            .LessThanOrEqualTo(MaxCooldown)
            .WithMessage($"Cooldown cannot exceed {MaxCooldown.TotalDays} days");
    }
}
