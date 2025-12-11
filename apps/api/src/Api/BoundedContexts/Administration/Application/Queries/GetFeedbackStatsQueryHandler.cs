using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetFeedbackStatsQuery.
/// Aggregates agent feedback data for analytics dashboard.
/// </summary>
public class GetFeedbackStatsQueryHandler : IQueryHandler<GetFeedbackStatsQuery, FeedbackStatsDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetFeedbackStatsQueryHandler> _logger;

    public GetFeedbackStatsQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetFeedbackStatsQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FeedbackStatsDto> Handle(GetFeedbackStatsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting feedback stats: StartDate={StartDate}, EndDate={EndDate}, Endpoint={Endpoint}",
            request.StartDate,
            request.EndDate,
            request.Endpoint);

        var query = _db.AgentFeedbacks
            .AsNoTracking() // PERF-05: Read-only analytics query
            .AsQueryable();

        // Apply filters
        if (request.StartDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            query = query.Where(f => f.Endpoint == request.Endpoint);
        }

        // PERF-03: Single aggregate query for main stats
        var stats = await query
            .GroupBy(_ => 1) // Group all into single aggregate
            .Select(g => new
            {
                TotalFeedbacks = g.Count(),
                HelpfulCount = g.Count(f => f.Outcome == "helpful"),
                NotHelpfulCount = g.Count(f => f.Outcome == "not-helpful")
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Separate query for endpoint breakdown
        var feedbackByEndpoint = await query
            .GroupBy(f => f.Endpoint)
            .Select(g => new { Endpoint = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Separate query for outcome breakdown
        var feedbackByOutcome = await query
            .GroupBy(f => f.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (stats == null)
        {
            // No data found - return empty stats
            _logger.LogInformation("No feedback data found for the given criteria");

            return new FeedbackStatsDto
            {
                TotalFeedbacks = 0,
                HelpfulCount = 0,
                NotHelpfulCount = 0,
                HelpfulRate = 0.0,
                FeedbackByEndpoint = new Dictionary<string, int>(StringComparer.Ordinal),
                FeedbackByOutcome = new Dictionary<string, int>(StringComparer.Ordinal)
            };
        }

        var helpfulRate = stats.TotalFeedbacks > 0
            ? (double)stats.HelpfulCount / stats.TotalFeedbacks
            : 0.0;

        _logger.LogInformation(
            "Feedback stats retrieved: Total={Total}, Helpful={Helpful}, Rate={Rate:P2}",
            stats.TotalFeedbacks,
            stats.HelpfulCount,
            helpfulRate);

        return new FeedbackStatsDto
        {
            TotalFeedbacks = stats.TotalFeedbacks,
            HelpfulCount = stats.HelpfulCount,
            NotHelpfulCount = stats.NotHelpfulCount,
            HelpfulRate = helpfulRate,
            FeedbackByEndpoint = feedbackByEndpoint.ToDictionary(x => x.Endpoint, x => x.Count, StringComparer.Ordinal),
            FeedbackByOutcome = feedbackByOutcome.ToDictionary(x => x.Outcome, x => x.Count, StringComparer.Ordinal)
        };
    }
}
