namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>Simple data transfer record used within the domain layer.</summary>
public record UserHandSlotData(
    string SlotType,
    Guid? EntityId,
    string? EntityType,
    string? EntityLabel,
    string? EntityImageUrl,
    DateTime? PinnedAt
);
