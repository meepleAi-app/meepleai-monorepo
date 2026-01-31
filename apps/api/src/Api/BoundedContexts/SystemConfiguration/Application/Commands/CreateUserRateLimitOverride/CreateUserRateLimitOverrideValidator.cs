using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.CreateUserRateLimitOverride;

/// <summary>
/// Validator for CreateUserRateLimitOverrideCommand.
/// Ensures user exists, reason is provided, and at least one override value is specified.
/// </summary>
internal sealed class CreateUserRateLimitOverrideValidator : AbstractValidator<CreateUserRateLimitOverrideCommand>
{
    private const int MinReasonLength = 10;
    private const int MaxReasonLength = 500;
    private const int MinLimitValue = 1;

    public CreateUserRateLimitOverrideValidator(IUserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(userRepository);

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .MustAsync(async (userId, ct) => await userRepository.ExistsAsync(userId, ct).ConfigureAwait(false))
            .WithMessage("User not found");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required")
            .MinimumLength(MinReasonLength)
            .WithMessage($"Reason must be at least {MinReasonLength} characters")
            .MaximumLength(MaxReasonLength)
            .WithMessage($"Reason cannot exceed {MaxReasonLength} characters");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiration date must be in the future");

        RuleFor(x => x.MaxPendingRequests)
            .GreaterThanOrEqualTo(MinLimitValue)
            .When(x => x.MaxPendingRequests.HasValue)
            .WithMessage($"Max pending requests must be at least {MinLimitValue}");

        RuleFor(x => x.MaxRequestsPerMonth)
            .GreaterThanOrEqualTo(MinLimitValue)
            .When(x => x.MaxRequestsPerMonth.HasValue)
            .WithMessage($"Max requests per month must be at least {MinLimitValue}");

        RuleFor(x => x.CooldownAfterRejection)
            .GreaterThanOrEqualTo(TimeSpan.Zero)
            .When(x => x.CooldownAfterRejection.HasValue)
            .WithMessage("Cooldown cannot be negative");

        // Business rule: At least one override value must be specified
        RuleFor(x => x)
            .Must(x => x.MaxPendingRequests.HasValue ||
                       x.MaxRequestsPerMonth.HasValue ||
                       x.CooldownAfterRejection.HasValue)
            .WithMessage("At least one limit override (MaxPending, MaxPerMonth, or Cooldown) must be specified");
    }
}
