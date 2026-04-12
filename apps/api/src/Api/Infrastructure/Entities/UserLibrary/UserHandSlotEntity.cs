namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// Persistence entity for "La Mia Mano" hand slots.
/// Each user has up to 4 slots (one per SlotType).
/// </summary>
public class UserHandSlotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this slot.</summary>
    public Guid UserId { get; set; }

    /// <summary>Slot type: "toolkit" | "game" | "session" | "ai"</summary>
    public string SlotType { get; set; } = string.Empty;

    /// <summary>ID of the pinned entity (null = empty slot).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>MeepleCard entity type: "toolkit" | "game" | "session" | "agent"</summary>
    public string? EntityType { get; set; }

    /// <summary>Cached display name for quick rendering.</summary>
    public string? EntityLabel { get; set; }

    /// <summary>Cached thumbnail URL.</summary>
    public string? EntityImageUrl { get; set; }

    /// <summary>When the slot was last assigned.</summary>
    public DateTime? PinnedAt { get; set; }

    // Navigation
    public UserEntity? User { get; set; }
}
