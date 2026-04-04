using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to add a new event to the session timeline (Session Diary).
/// Requires at least Player role - spectators cannot add events.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public record AddSessionEventCommand(
    Guid SessionId,
    Guid RequesterId,
    string EventType,
    string? Payload = null,
    string? Source = null
) : IRequest<AddSessionEventResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Player.</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Result of adding a session event.
/// </summary>
public record AddSessionEventResult(
    Guid EventId,
    string EventType,
    DateTime Timestamp
);

/// <summary>
/// FluentValidation validator for AddSessionEventCommand.
/// </summary>
public class AddSessionEventCommandValidator : AbstractValidator<AddSessionEventCommand>
{
    public AddSessionEventCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage("EventType is required")
            .MaximumLength(50)
            .WithMessage("EventType must be 50 characters or fewer");

        RuleFor(x => x.Source)
            .MaximumLength(50)
            .WithMessage("Source must be 50 characters or fewer")
            .When(x => x.Source != null);
    }
}
