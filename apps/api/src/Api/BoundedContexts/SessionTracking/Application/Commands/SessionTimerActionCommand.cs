using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Unified timer action command for start, pause, resume, and reset.
/// Requires at least Player role — spectators cannot control the session timer.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record SessionTimerActionCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId,
    TimerAction Action,
    string ParticipantName = "",
    int DurationSeconds = 60
) : IRequest<SessionTimerActionResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Player.</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Available timer action types.
/// </summary>
public enum TimerAction
{
    /// <summary>Start a new countdown timer.</summary>
    Start = 0,
    /// <summary>Pause a running timer.</summary>
    Pause = 1,
    /// <summary>Resume a paused timer.</summary>
    Resume = 2,
    /// <summary>Reset the timer.</summary>
    Reset = 3
}

/// <summary>
/// Result of a timer action.
/// </summary>
public record SessionTimerActionResult(
    Guid SessionId,
    TimerAction Action,
    string Status,
    int RemainingSeconds,
    DateTime UpdatedAt
);

/// <summary>
/// FluentValidation validator for SessionTimerActionCommand.
/// </summary>
public class SessionTimerActionCommandValidator : AbstractValidator<SessionTimerActionCommand>
{
    public SessionTimerActionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.Action)
            .IsInEnum()
            .WithMessage("Action must be a valid TimerAction (Start, Pause, Resume, Reset)");

        RuleFor(x => x.DurationSeconds)
            .GreaterThan(0)
            .WithMessage("DurationSeconds must be positive")
            .LessThanOrEqualTo(3600)
            .WithMessage("DurationSeconds must be 3600 (1 hour) or fewer")
            .When(x => x.Action == TimerAction.Start);
    }
}
