using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to start a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// Issue #2587: Added UserId/UserTier/UserRole for quota enforcement and GameSession correlation at start.
/// </summary>
internal record StartLiveSessionCommand(
    Guid SessionId,
    Guid UserId,
    UserTier UserTier,
    Role UserRole
) : ICommand;
