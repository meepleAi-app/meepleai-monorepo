using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IPauseSnapshotRepository.
/// Persists full-state pause snapshots to the pause_snapshots table.
/// JSONB columns (PlayerScoresJson, AttachmentIdsJson, DisputesJson) are
/// serialised/deserialised via System.Text.Json.
/// </summary>
internal sealed class PauseSnapshotRepository : IPauseSnapshotRepository
{
    // CA1869: cache options instance for performance
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MeepleAiDbContext _dbContext;

    public PauseSnapshotRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PauseSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.PauseSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<PauseSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entity = await _dbContext.PauseSnapshots
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == sessionId)
            .OrderByDescending(e => e.SavedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<PauseSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await _dbContext.PauseSnapshots
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == sessionId)
            .OrderBy(e => e.SavedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(PauseSnapshot snapshot, CancellationToken ct = default)
    {
        var entity = MapToPersistence(snapshot);
        await _dbContext.PauseSnapshots.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(PauseSnapshot snapshot, CancellationToken ct = default)
    {
        var entity = MapToPersistence(snapshot);
        _dbContext.PauseSnapshots.Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAutoSavesBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var autoSaves = await _dbContext.PauseSnapshots
            .Where(e => e.LiveGameSessionId == sessionId && e.IsAutoSave)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (autoSaves.Count > 0)
            _dbContext.PauseSnapshots.RemoveRange(autoSaves);
    }

    private PauseSnapshot MapToDomain(PauseSnapshotEntity entity)
    {
        var playerScores = JsonSerializer
            .Deserialize<List<PlayerScoreSnapshot>>(entity.PlayerScoresJson, JsonOptions)
            ?? new List<PlayerScoreSnapshot>();

        var attachmentIds = JsonSerializer
            .Deserialize<List<Guid>>(entity.AttachmentIdsJson, JsonOptions)
            ?? new List<Guid>();

        var disputes = JsonSerializer
            .Deserialize<List<RuleDisputeEntry>>(entity.DisputesJson, JsonOptions)
            ?? new List<RuleDisputeEntry>();

        return PauseSnapshot.Restore(
            id: entity.Id,
            liveGameSessionId: entity.LiveGameSessionId,
            currentTurn: entity.CurrentTurn,
            currentPhase: entity.CurrentPhase,
            playerScores: playerScores,
            savedByUserId: entity.SavedByUserId,
            isAutoSave: entity.IsAutoSave,
            savedAt: entity.SavedAt,
            attachmentIds: attachmentIds,
            disputes: disputes,
            gameStateJson: entity.GameStateJson,
            agentConversationSummary: entity.AgentConversationSummary);
    }

    private PauseSnapshotEntity MapToPersistence(PauseSnapshot snapshot)
    {
        return new PauseSnapshotEntity
        {
            Id = snapshot.Id,
            LiveGameSessionId = snapshot.LiveGameSessionId,
            CurrentTurn = snapshot.CurrentTurn,
            CurrentPhase = snapshot.CurrentPhase,
            PlayerScoresJson = JsonSerializer.Serialize(snapshot.PlayerScores, JsonOptions),
            AttachmentIdsJson = JsonSerializer.Serialize(snapshot.AttachmentIds, JsonOptions),
            DisputesJson = JsonSerializer.Serialize(snapshot.Disputes, JsonOptions),
            AgentConversationSummary = snapshot.AgentConversationSummary,
            GameStateJson = snapshot.GameStateJson,
            SavedAt = snapshot.SavedAt,
            SavedByUserId = snapshot.SavedByUserId,
            IsAutoSave = snapshot.IsAutoSave
        };
    }

}
