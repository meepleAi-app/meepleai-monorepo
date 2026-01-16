using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for library quota information exposed via API (Issue #2445).
/// Property names match frontend contract exactly.
/// </summary>
/// <param name="CurrentCount">Number of games currently in the user's library.</param>
/// <param name="MaxAllowed">Maximum number of games allowed for this user/tier.</param>
/// <param name="UserTier">User's current tier (free/normal/premium).</param>
/// <param name="RemainingSlots">Number of remaining slots available to add games.</param>
/// <param name="PercentageUsed">Percentage of quota used (0-100).</param>
public record LibraryQuotaDto(
    [property: JsonPropertyName("currentCount")] int CurrentCount,
    [property: JsonPropertyName("maxAllowed")] int MaxAllowed,
    [property: JsonPropertyName("userTier")] string UserTier,
    [property: JsonPropertyName("remainingSlots")] int RemainingSlots,
    [property: JsonPropertyName("percentageUsed")] int PercentageUsed
);
