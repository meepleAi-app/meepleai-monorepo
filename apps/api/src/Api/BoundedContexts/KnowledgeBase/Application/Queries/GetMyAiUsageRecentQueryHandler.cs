using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetMyAiUsageRecentQuery.
/// Returns paginated recent AI requests (last 7 days only).
/// Security: excludes ErrorMessage, IpAddress, UserAgent, SessionId.
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
internal class GetMyAiUsageRecentQueryHandler
    : IQueryHandler<GetMyAiUsageRecentQuery, AiUsageRecentDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetMyAiUsageRecentQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AiUsageRecentDto> Handle(
        GetMyAiUsageRecentQuery query, CancellationToken cancellationToken)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var baseQuery = _dbContext.LlmCostLogs
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId && x.CreatedAt >= sevenDaysAgo)
            .OrderByDescending(x => x.CreatedAt);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecentAiRequestDto(
                x.CreatedAt,
                x.ModelId,
                x.Provider,
                x.Endpoint,
                x.PromptTokens,
                x.CompletionTokens,
                x.TotalCost,
                x.LatencyMs,
                x.Success
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AiUsageRecentDto(
            items,
            total,
            page,
            pageSize,
            "Individual requests are available for the last 7 days only (GDPR compliance)."
        );
    }
}
