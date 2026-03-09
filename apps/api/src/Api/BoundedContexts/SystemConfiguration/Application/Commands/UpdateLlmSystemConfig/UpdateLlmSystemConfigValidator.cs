using System.Text.Json;

using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;

/// <summary>
/// Issue #5495: Validation for UpdateLlmSystemConfigCommand.
/// </summary>
internal sealed class UpdateLlmSystemConfigValidator : AbstractValidator<UpdateLlmSystemConfigCommand>
{
    public UpdateLlmSystemConfigValidator()
    {
        RuleFor(x => x.CircuitBreakerFailureThreshold)
            .GreaterThanOrEqualTo(1).WithMessage("FailureThreshold must be >= 1")
            .LessThanOrEqualTo(100).WithMessage("FailureThreshold must be <= 100");

        RuleFor(x => x.CircuitBreakerOpenDurationSeconds)
            .GreaterThanOrEqualTo(1).WithMessage("OpenDurationSeconds must be >= 1")
            .LessThanOrEqualTo(3600).WithMessage("OpenDurationSeconds must be <= 3600 (1 hour)");

        RuleFor(x => x.CircuitBreakerSuccessThreshold)
            .GreaterThanOrEqualTo(1).WithMessage("SuccessThreshold must be >= 1")
            .LessThanOrEqualTo(100).WithMessage("SuccessThreshold must be <= 100");

        RuleFor(x => x.DailyBudgetUsd)
            .GreaterThanOrEqualTo(0).WithMessage("DailyBudgetUsd cannot be negative")
            .LessThanOrEqualTo(10_000m).WithMessage("DailyBudgetUsd must be <= $10,000");

        RuleFor(x => x.MonthlyBudgetUsd)
            .GreaterThanOrEqualTo(0).WithMessage("MonthlyBudgetUsd cannot be negative")
            .LessThanOrEqualTo(100_000m).WithMessage("MonthlyBudgetUsd must be <= $100,000");

        RuleFor(x => x.DailyBudgetUsd)
            .LessThanOrEqualTo(x => x.MonthlyBudgetUsd)
            .WithMessage("DailyBudgetUsd cannot exceed MonthlyBudgetUsd");

        RuleFor(x => x.FallbackChainJson)
            .NotNull().WithMessage("FallbackChainJson is required")
            .NotEmpty().WithMessage("FallbackChainJson cannot be empty")
            .Must(BeValidJsonArray).WithMessage("FallbackChainJson must be a valid JSON array");

        RuleFor(x => x.UpdatedByUserId)
            .NotEqual(Guid.Empty).WithMessage("UpdatedByUserId is required");
    }

    private static bool BeValidJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
