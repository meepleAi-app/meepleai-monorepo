namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

public interface IUserHandRepository
{
    Task<IReadOnlyList<UserHandSlotData>> GetAllSlotsAsync(Guid userId, CancellationToken ct = default);
    Task UpsertSlotAsync(Guid userId, string slotType, Guid entityId, string entityType, string? entityLabel, string? entityImageUrl, CancellationToken ct = default);
    Task ClearSlotAsync(Guid userId, string slotType, CancellationToken ct = default);
}
