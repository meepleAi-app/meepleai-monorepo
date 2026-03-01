using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for AdvanceLiveSessionPhaseCommand.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class AdvanceLiveSessionPhaseCommandValidator : AbstractValidator<AdvanceLiveSessionPhaseCommand>
{
    public AdvanceLiveSessionPhaseCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");
    }
}

/// <summary>
/// Validator for ConfigureLiveSessionPhasesCommand.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class ConfigureLiveSessionPhasesCommandValidator : AbstractValidator<ConfigureLiveSessionPhasesCommand>
{
    public ConfigureLiveSessionPhasesCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.PhaseNames)
            .NotNull().WithMessage("Phase names are required")
            .Must(p => p.Length > 0).WithMessage("At least one phase name is required")
            .Must(p => p.Length <= 20).WithMessage("Cannot have more than 20 phases")
            .Must(p => p.Any(name => !string.IsNullOrWhiteSpace(name)))
            .WithMessage("At least one non-empty phase name is required");

        RuleForEach(x => x.PhaseNames)
            .NotEmpty().WithMessage("Phase name cannot be empty")
            .MaximumLength(100).WithMessage("Phase name cannot exceed 100 characters");
    }
}

/// <summary>
/// Validator for TriggerEventSnapshotCommand.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal sealed class TriggerEventSnapshotCommandValidator : AbstractValidator<TriggerEventSnapshotCommand>
{
    public TriggerEventSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.TriggerType)
            .IsInEnum().WithMessage("Invalid trigger type");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null)
            .WithMessage("Description cannot exceed 500 characters");
    }
}
