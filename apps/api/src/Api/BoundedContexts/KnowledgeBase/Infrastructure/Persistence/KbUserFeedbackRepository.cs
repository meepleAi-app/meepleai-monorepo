using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

internal sealed class KbUserFeedbackRepository : IKbUserFeedbackRepository
{
    private const string Helpful = "helpful";
    private const string NotHelpful = "not_helpful";

    private readonly MeepleAiDbContext _db;

    public KbUserFeedbackRepository(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default)
    {
        await _db.KbUserFeedbacks.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KbUserFeedback>> ListAsync(
        KbFeedbackFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await Filtered(filter)
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountAsync(KbFeedbackFilter filter, CancellationToken cancellationToken = default)
        => Filtered(filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<KbFeedbackVersionCount>> CountOutcomesByVersionAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.KbUserFeedbacks
            .AsNoTracking()
            .GroupBy(f => f.ProfileVersionId)
            .Select(g => new KbFeedbackVersionCount(
                g.Key,
                g.Count(f => f.Outcome == Helpful),
                g.Count(f => f.Outcome == NotHelpful)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetGameIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.KbUserFeedbacks
            .AsNoTracking()
            .Select(f => f.GameId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return _db.KbUserFeedbacks
            .AsNoTracking()
            .Where(f => f.CreatedAt >= since)
            .CountAsync(cancellationToken);
    }

    private IQueryable<KbUserFeedback> Filtered(KbFeedbackFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _db.KbUserFeedbacks.AsNoTracking();
        if (filter.GameId is { } gameId)
            query = query.Where(f => f.GameId == gameId);
        if (filter.WithoutVersion)
            query = query.Where(f => f.ProfileVersionId == null);
        else if (filter.ProfileVersionId is { } versionId)
            query = query.Where(f => f.ProfileVersionId == versionId);
        if (filter.Outcome is not null)
            query = query.Where(f => f.Outcome == filter.Outcome);
        if (filter.From is { } from)
            query = query.Where(f => f.CreatedAt >= from);
        return query;
    }
}
