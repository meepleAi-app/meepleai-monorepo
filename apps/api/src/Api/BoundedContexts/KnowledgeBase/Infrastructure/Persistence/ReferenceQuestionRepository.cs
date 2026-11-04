using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

internal sealed class ReferenceQuestionRepository : IReferenceQuestionRepository
{
    private readonly MeepleAiDbContext _db;

    public ReferenceQuestionRepository(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<ReferenceQuestion>> ListAsync(
        Guid? sharedGameId, bool? inGate, CancellationToken cancellationToken = default)
    {
        var query = _db.ReferenceQuestions.AsNoTracking();
        if (sharedGameId is { } gameId)
            query = query.Where(q => q.SharedGameId == gameId);
        if (inGate is { } gate)
            query = query.Where(q => q.InGate == gate);

        return await query
            .OrderBy(q => q.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByGameAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ReferenceQuestions
            .AsNoTracking()
            .GroupBy(q => q.SharedGameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GameId, g => g.Count, cancellationToken)
            .ConfigureAwait(false);
    }
}
