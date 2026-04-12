namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO representing a single slot in the user's hand (quick-access bar).
/// </summary>
public record UserHandSlotDto(
    string SlotType,
    Guid? EntityId,
    string? EntityType,
    string? EntityLabel,
    string? EntityImageUrl,
    string? PinnedAt  // ISO 8601
);
