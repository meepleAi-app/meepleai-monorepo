namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Issue #2036 — Session contributor (= registered user who participated in at least one
/// finalized session for a given game), used by the ContributorsStrip avatar
/// stack on game-detail pages.
/// </summary>
/// <param name="UserId">Identifier of the registered user.</param>
/// <param name="DisplayName">Public display name (from <c>users.display_name</c>).</param>
/// <param name="Initials">1–2 uppercase letters derived from the display name; the
/// strip uses this as a fallback when an avatar image is missing.</param>
/// <param name="SessionCount">Number of distinct finalized sessions of the game
/// in which the user appears as a participant.</param>
public record SessionContributorDto(
    Guid UserId,
    string DisplayName,
    string Initials,
    int SessionCount
);
