using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetLowQualityResponsesQuery.
/// Retrieves low-quality AI responses for admin review.
/// AI-11: Quality tracking endpoints
/// NOTE: Uses DbContext directly for AiRequestLogs (legacy infrastructure entity).
/// ISSUE-1674: Create proper repository when AiRequestLog is migrated to bounded context.
/// </summary>
public class GetLowQualityResponsesQueryHandler : IQueryHandler<GetLowQualityResponsesQuery, LowQualityResponsesResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetLowQualityResponsesQueryHandler> _logger;

    public GetLowQualityResponsesQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetLowQualityResponsesQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<LowQualityResponsesResult> Handle(GetLowQualityResponsesQuery query, CancellationToken cancellationToken)
    {
        try
        {
            // Convert DateTime parameters to UTC if they have Kind=Unspecified (from query string parsing)
            // PostgreSQL requires UTC DateTimes
            var startDate = query.StartDate;
            var endDate = query.EndDate;

            if (startDate.HasValue && startDate.Value.Kind == DateTimeKind.Unspecified)
                startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            if (endDate.HasValue && endDate.Value.Kind == DateTimeKind.Unspecified)
                endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);

            // Query low-quality responses with filters
            var dbQuery = _dbContext.AiRequestLogs
                .AsNoTracking()
                .Where(log => log.IsLowQuality);

            if (startDate.HasValue)
                dbQuery = dbQuery.Where(log => log.CreatedAt >= startDate.Value);
            if (endDate.HasValue)
                dbQuery = dbQuery.Where(log => log.CreatedAt <= endDate.Value);

            var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
            var responses = await dbQuery
                .OrderByDescending(log => log.CreatedAt)
                .Skip(query.Offset)
                .Take(query.Limit)
                .Select(log => new LowQualityResponseDto(
                    log.Id,
                    log.CreatedAt,
                    log.Query ?? string.Empty,
                    log.RagConfidence ?? 0.0,
                    log.LlmConfidence ?? 0.0,
                    log.CitationQuality ?? 0.0,
                    log.OverallConfidence ?? 0.0,
                    log.IsLowQuality
                ))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Retrieved {Count} low-quality responses (total: {TotalCount})", responses.Count, totalCount);

            return new LowQualityResponsesResult(
                TotalCount: totalCount,
                Responses: responses
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low-quality responses");
            return new LowQualityResponsesResult(
                TotalCount: 0,
                Responses: Array.Empty<LowQualityResponseDto>()
            );
        }
    }
}
