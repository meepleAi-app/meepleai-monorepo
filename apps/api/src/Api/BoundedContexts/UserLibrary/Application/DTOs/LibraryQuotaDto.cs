namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for library quota information exposed via API.
/// </summary>
/// <param name="GamesInLibrary">Number of games currently in the user's library.</param>
/// <param name="MaxGames">Maximum number of games allowed for this user/tier.</param>
/// <param name="RemainingSlots">Number of remaining slots available to add games.</param>
/// <param name="IsUnlimited">Whether the user has unlimited game slots (Admin/Editor).</param>
/// <param name="UserTier">User's current tier (free/normal/premium).</param>
public record LibraryQuotaDto(
    int GamesInLibrary,
    int MaxGames,
    int RemainingSlots,
    bool IsUnlimited,
    string UserTier
);
