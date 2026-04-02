using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

internal sealed class KbUserFeedbackRepository : IKbUserFeedbackRepository
{
    private readonly MeepleAiDbContext _db;

    public KbUserFeedbackRepository(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default)
    {
        await _db.KbUserFeedbacks.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KbUserFeedback>> GetByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.KbUserFeedbacks.AsNoTracking().Where(f => f.GameId == gameId);
        if (outcomeFilter is not null)
            query = query.Where(f => f.Outcome == outcomeFilter);
        if (fromDate.HasValue)
            query = query.Where(f => f.CreatedAt >= fromDate.Value);
        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        CancellationToken cancellationToken = default)
    {
        var query = _db.KbUserFeedbacks.AsNoTracking().Where(f => f.GameId == gameId);
        if (outcomeFilter is not null)
            query = query.Where(f => f.Outcome == outcomeFilter);
        if (fromDate.HasValue)
            query = query.Where(f => f.CreatedAt >= fromDate.Value);
        return query.CountAsync(cancellationToken);
    }
}
