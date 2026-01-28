using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to start a new game session.
/// Issue #3070: Added UserId, UserTier, and UserRole for session quota enforcement.
/// </summary>
internal record StartGameSessionCommand(
    Guid GameId,
    IReadOnlyList<SessionPlayerRequest> Players,
    Guid UserId,
    UserTier UserTier,
    Role UserRole
) : ICommand<GameSessionDto>;
