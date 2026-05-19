namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// DTO representing a regular co-participant: a registered user the current
/// organizer has invited to past game nights in the last 12 months.
/// Issue #950 (W1-PR2) — feeds the Step 3 "Chi" suggestion list in the
/// game night wizard. Spec §7b.2.
/// </summary>
/// <param name="Id">User identifier.</param>
/// <param name="DisplayName">User's display name (fallback to email if null).</param>
/// <param name="Email">User's email address.</param>
/// <param name="EventCount">Number of distinct game nights the organizer invited this user to in the window.</param>
/// <param name="LastInvitedAt">Timestamp of the most recent invitation.</param>
public sealed record RegularDto(
    Guid Id,
    string DisplayName,
    string Email,
    int EventCount,
    DateTimeOffset LastInvitedAt);
