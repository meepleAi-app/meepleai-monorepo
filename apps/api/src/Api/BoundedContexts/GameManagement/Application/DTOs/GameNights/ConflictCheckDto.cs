namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// DTO representing the result of a game-night scheduling conflict check.
/// Issue #950 (W1-PR2). Spec §7b.3.
/// </summary>
/// <param name="HasConflict">True iff at least one conflicting event was found.</param>
/// <param name="Conflicts">Conflicting events within ±2 hours of the proposed time.</param>
public sealed record ConflictCheckDto(
    bool HasConflict,
    IReadOnlyList<ConflictEntryDto> Conflicts);

/// <summary>
/// Single conflicting game-night entry returned by the check-conflict endpoint.
/// </summary>
/// <param name="Id">Conflicting event identifier.</param>
/// <param name="Title">Conflicting event title.</param>
/// <param name="ScheduledAt">Conflicting event start time.</param>
/// <param name="Role">User's role on the conflicting event (<c>organizer</c> or <c>invitee</c>).</param>
public sealed record ConflictEntryDto(
    Guid Id,
    string Title,
    DateTimeOffset ScheduledAt,
    string Role);
