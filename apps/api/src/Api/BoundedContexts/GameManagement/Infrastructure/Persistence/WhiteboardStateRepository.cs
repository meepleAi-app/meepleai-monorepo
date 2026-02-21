using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the WhiteboardState repository.
/// Maps between the domain WhiteboardState entity and WhiteboardStateEntity persistence model.
/// Strokes are stored as a JSONB array; the domain aggregate is reconstructed via Restore().
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class WhiteboardStateRepository : IWhiteboardStateRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public WhiteboardStateRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<WhiteboardState?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.WhiteboardStates
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(WhiteboardState whiteboardState, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(whiteboardState);
        await _dbContext.WhiteboardStates.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(WhiteboardState whiteboardState, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(whiteboardState);

        var tracked = _dbContext.ChangeTracker.Entries<WhiteboardStateEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        _dbContext.WhiteboardStates.Update(entity);
        return Task.CompletedTask;
    }

    private static WhiteboardState MapToDomain(WhiteboardStateEntity entity)
    {
        var strokes = JsonSerializer.Deserialize<List<WhiteboardStrokeSerializable>>(entity.StrokesJson)
            ?? new List<WhiteboardStrokeSerializable>();

        var domainStrokes = strokes.Select(s => new WhiteboardStroke(s.Id, s.DataJson));

        return WhiteboardState.Restore(
            id: entity.Id,
            sessionId: entity.SessionId,
            strokes: domainStrokes,
            structuredJson: entity.StructuredJson,
            lastModifiedBy: entity.LastModifiedBy,
            lastModifiedAt: entity.LastModifiedAt,
            createdAt: entity.CreatedAt);
    }

    private static WhiteboardStateEntity MapToPersistence(WhiteboardState whiteboardState)
    {
        var strokesJson = JsonSerializer.Serialize(
            whiteboardState.Strokes
                .Select(s => new WhiteboardStrokeSerializable { Id = s.Id, DataJson = s.DataJson })
                .ToList());

        return new WhiteboardStateEntity
        {
            Id = whiteboardState.Id,
            SessionId = whiteboardState.SessionId,
            StrokesJson = strokesJson,
            StructuredJson = whiteboardState.StructuredJson,
            LastModifiedBy = whiteboardState.LastModifiedBy,
            LastModifiedAt = whiteboardState.LastModifiedAt,
            CreatedAt = whiteboardState.CreatedAt
        };
    }

    /// <summary>Plain serialization model for whiteboard strokes (avoids internal record constraints).</summary>
    private sealed class WhiteboardStrokeSerializable
    {
        public string Id { get; set; } = string.Empty;
        public string DataJson { get; set; } = "{}";
    }
}
