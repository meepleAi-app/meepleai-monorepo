namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// #4780 PR 4: an email invitation accepted without an account. <see cref="RespondedByName"/> is the name typed on the
/// answer by someone not logged in, and <see cref="Email"/> the address the invitation was sent to: both raw, so a
/// reader turns them into a table name before anyone sees them.
/// </summary>
internal sealed record AcceptedGuestWithoutAccount(Guid InvitationId, string? RespondedByName, string Email);
