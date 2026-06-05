namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// Asse C (#1898) WP1 T1 DEC-2: Friend activity entry for dashboard
/// "Cosa fanno i tuoi" section. Friend qualification per MAJ-5:
/// User-linked players con almeno 1 shared GameNight last 90gg.
/// </summary>
/// <param name="FriendUserId">The friend's user id.</param>
/// <param name="Avatar">The friend's avatar URL (empty string when unset).</param>
/// <param name="Name">The friend's display name or email-local-part fallback.</param>
/// <param name="Verb">Activity verb: "completed" | "created" | "joined".</param>
/// <param name="GameOrEventId">The related game-night or game entity id.</param>
/// <param name="GameOrEventType">Type discriminator: "game" | "gameNight".</param>
/// <param name="GameOrEventName">Human-readable label of the related entity.</param>
/// <param name="Timestamp">Activity timestamp (UTC).</param>
public sealed record FriendActivityDto(
    Guid FriendUserId,
    string Avatar,
    string Name,
    string Verb,
    Guid GameOrEventId,
    string GameOrEventType,
    string GameOrEventName,
    DateTimeOffset Timestamp);
