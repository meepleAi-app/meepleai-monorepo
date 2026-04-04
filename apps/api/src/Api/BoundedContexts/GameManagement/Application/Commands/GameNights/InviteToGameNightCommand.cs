using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to invite additional users to a published game night.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record InviteToGameNightCommand(
    Guid GameNightId,
    Guid UserId,
    List<Guid> InvitedUserIds
) : ICommand;
