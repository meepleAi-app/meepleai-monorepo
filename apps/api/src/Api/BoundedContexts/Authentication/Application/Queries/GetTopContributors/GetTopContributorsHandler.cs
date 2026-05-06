using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;

/// <summary>
/// Handler for <see cref="GetTopContributorsQuery"/>.
///
/// Ranking formula (equal-weight sum):
///   ContributionCount = kbUploads (public PdfDocuments uploaded by user)
///                     + distinctAgentSessions (distinct AgentDefinitionIds per user)
///
/// faqCount is excluded: GameFaqEntity has no user FK, so per-user FAQ count
/// is uncomputable. Suspended users and zero-contribution users are excluded.
///
/// Issue #728.
/// </summary>
internal sealed class GetTopContributorsHandler
    : IQueryHandler<GetTopContributorsQuery, IReadOnlyList<TopContributorDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopContributorsHandler> _logger;

    public GetTopContributorsHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetTopContributorsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TopContributorDto>> Handle(
        GetTopContributorsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, 20);

        _logger.LogDebug("Fetching top {Limit} contributors", limit);

        // Project per-user aggregates in a single round-trip.
        // EF Core translates the subquery COUNT() calls to correlated SQL aggregates.
        // GameFaqEntity excluded: no user FK available on that entity.
        var results = await (
            from u in _dbContext.Users.AsNoTracking()
            where !u.IsSuspended
            let kbCount = _dbContext.PdfDocuments
                .Count(p => p.UploadedByUserId == u.Id && p.IsPublic)
            let agentCount = _dbContext.AgentSessions
                .Where(a => a.UserId == u.Id)
                .Select(a => a.AgentDefinitionId)
                .Distinct()
                .Count()
            let total = kbCount + agentCount
            where total > 0
            orderby total descending, u.DisplayName
            select new TopContributorDto(
                u.Id,
                u.DisplayName ?? string.Empty,
                u.AvatarUrl,
                total,
                kbCount,
                agentCount)
        )
        .Take(limit)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

        _logger.LogDebug("Top contributors query returned {Count} results", results.Count);

        return results;
    }
}
