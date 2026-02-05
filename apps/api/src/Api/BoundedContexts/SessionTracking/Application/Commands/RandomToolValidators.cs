using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Validators for random tool commands (Issue #3345).
/// </summary>

public sealed class StartTimerCommandValidator : AbstractValidator<StartTimerCommand>
{
    public StartTimerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");

        RuleFor(x => x.ParticipantName)
            .NotEmpty()
            .WithMessage("Participant name is required")
            .MaximumLength(100)
            .WithMessage("Participant name must not exceed 100 characters");

        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0)
            .WithMessage("Duration must be greater than 0")
            .LessThanOrEqualTo(3600)
            .WithMessage("Duration must not exceed 1 hour (3600 seconds)");
    }
}

public sealed class PauseTimerCommandValidator : AbstractValidator<PauseTimerCommand>
{
    public PauseTimerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");
    }
}

public sealed class ResumeTimerCommandValidator : AbstractValidator<ResumeTimerCommand>
{
    public ResumeTimerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");
    }
}

public sealed class ResetTimerCommandValidator : AbstractValidator<ResetTimerCommand>
{
    public ResetTimerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");
    }
}

public sealed class FlipCoinCommandValidator : AbstractValidator<FlipCoinCommand>
{
    public FlipCoinCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");

        RuleFor(x => x.ParticipantName)
            .NotEmpty()
            .WithMessage("Participant name is required")
            .MaximumLength(100)
            .WithMessage("Participant name must not exceed 100 characters");
    }
}

public sealed class SpinWheelCommandValidator : AbstractValidator<SpinWheelCommand>
{
    public SpinWheelCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required");

        RuleFor(x => x.ParticipantName)
            .NotEmpty()
            .WithMessage("Participant name is required")
            .MaximumLength(100)
            .WithMessage("Participant name must not exceed 100 characters");

        RuleFor(x => x.Options)
            .NotEmpty()
            .WithMessage("At least one option is required")
            .Must(x => x.Count >= 2)
            .WithMessage("At least 2 options are required for the wheel");

        RuleForEach(x => x.Options)
            .ChildRules(option =>
            {
                option.RuleFor(o => o.Id)
                    .NotEmpty()
                    .WithMessage("Option ID is required");

                option.RuleFor(o => o.Label)
                    .NotEmpty()
                    .WithMessage("Option label is required")
                    .MaximumLength(50)
                    .WithMessage("Option label must not exceed 50 characters");

                option.RuleFor(o => o.Weight)
                    .GreaterThan(0)
                    .WithMessage("Option weight must be greater than 0");
            });
    }
}
