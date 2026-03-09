using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;

/// <summary>
/// Issue #5476: Validates emergency override activation requests.
/// </summary>
internal class ActivateEmergencyOverrideCommandValidator : AbstractValidator<ActivateEmergencyOverrideCommand>
{
    private static readonly string[] s_validActions =
    [
        "force-ollama-only",
        "reset-circuit-breaker",
        "flush-quota-cache"
    ];

    public ActivateEmergencyOverrideCommandValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required")
            .Must(a => s_validActions.Contains(a, StringComparer.Ordinal))
            .WithMessage($"Action must be one of: {string.Join(", ", s_validActions)}");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 1440)
            .WithMessage("Duration must be between 1 and 1440 minutes (24 hours)");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required for audit trail")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required");

        RuleFor(x => x.TargetProvider)
            .Must(p => p is null or "Ollama" or "OpenRouter")
            .WithMessage("TargetProvider must be 'Ollama' or 'OpenRouter' if specified");
    }
}

/// <summary>
/// Issue #5476: Validates emergency override deactivation requests.
/// </summary>
internal class DeactivateEmergencyOverrideCommandValidator : AbstractValidator<DeactivateEmergencyOverrideCommand>
{
    private static readonly string[] s_validActions =
    [
        "force-ollama-only",
        "reset-circuit-breaker",
        "flush-quota-cache"
    ];

    public DeactivateEmergencyOverrideCommandValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required")
            .Must(a => s_validActions.Contains(a, StringComparer.Ordinal))
            .WithMessage($"Action must be one of: {string.Join(", ", s_validActions)}");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required");
    }
}
