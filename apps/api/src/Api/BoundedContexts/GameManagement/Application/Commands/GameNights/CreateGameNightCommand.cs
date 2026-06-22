using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to create a new game night event in Draft status.
/// Issue #46: GameNight API endpoints.
/// Issue #950 (W1-PR1): added <c>InvitedEmails</c> for token-based
/// invitations to non-registered users via the existing
/// <c>GameNightInvitation</c> aggregate (issue #607 Wave A.5a).
/// </summary>
internal record CreateGameNightCommand(
    Guid UserId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Description = null,
    string? Location = null,
    int? MaxPlayers = null,
    List<Guid>? GameIds = null,
    List<Guid>? InvitedUserIds = null,
    List<string>? InvitedEmails = null
) : ICommand<Guid>;
