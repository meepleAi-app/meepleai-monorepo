using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to create a new game night event in Draft status.
/// Issue #46: GameNight API endpoints.
/// Issue #950 (W1-PR1): added <c>InvitedEmails</c> for token-based
/// invitations to non-registered users via the existing
/// <c>GameNightInvitation</c> aggregate (issue #607 Wave A.5a).
/// Issue #4692: the optional <c>Id</c> is chosen by the caller so that repeating the same creation (the
/// wizard retries, and so does the mutation layer) finds the game night it already made instead of making
/// another. The same id from the same organizer is the same creation; from another organizer it is a conflict.
/// Issue #4754: the optional <c>TimeZoneId</c> is the IANA zone the organizer scheduled in (the browser's),
/// kept so the emails can say 21:00 where the organizer meant 21:00 instead of the UTC instant.
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
    List<string>? InvitedEmails = null,
    Guid? Id = null,
    string? TimeZoneId = null
) : ICommand<Guid>;
