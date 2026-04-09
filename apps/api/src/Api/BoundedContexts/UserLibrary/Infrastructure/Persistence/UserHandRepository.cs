using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

internal class UserHandRepository : IUserHandRepository
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public UserHandRepository(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<UserHandSlotData>> GetAllSlotsAsync(Guid userId, CancellationToken ct = default)
    {
        var entities = await _db.UserHandSlots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => new UserHandSlotData(
            e.SlotType,
            e.EntityId,
            e.EntityType,
            e.EntityLabel,
            e.EntityImageUrl,
            e.PinnedAt
        )).ToList();
    }

    public async Task UpsertSlotAsync(Guid userId, string slotType, Guid entityId, string entityType,
        string? entityLabel, string? entityImageUrl, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var existing = await _db.UserHandSlots
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SlotType == slotType, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.UserHandSlots.Add(new UserHandSlotEntity
            {
                UserId = userId,
                SlotType = slotType,
                EntityId = entityId,
                EntityType = entityType,
                EntityLabel = entityLabel,
                EntityImageUrl = entityImageUrl,
                PinnedAt = now
            });
        }
        else
        {
            existing.EntityId = entityId;
            existing.EntityType = entityType;
            existing.EntityLabel = entityLabel;
            existing.EntityImageUrl = entityImageUrl;
            existing.PinnedAt = now;
        }
    }

    public async Task ClearSlotAsync(Guid userId, string slotType, CancellationToken ct = default)
    {
        var existing = await _db.UserHandSlots
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SlotType == slotType, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.UserHandSlots.Remove(existing);
        }
    }
}
