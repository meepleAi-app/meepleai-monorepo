using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to publish a draft game night, making it visible and sending invitations.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record PublishGameNightCommand(
    Guid GameNightId,
    Guid UserId
) : ICommand;
