using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for retrieving FAQs for a game with pagination.
/// Issue #2681: Public FAQs endpoints
/// </summary>
internal sealed class GetGameFaqsQueryHandler
    : IQueryHandler<GetGameFaqsQuery, GetGameFaqsResultDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetGameFaqsQueryHandler> _logger;

    public GetGameFaqsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetGameFaqsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetGameFaqsResultDto> Handle(
        GetGameFaqsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving FAQs for game: {GameId}, Limit: {Limit}, Offset: {Offset}",
            query.GameId,
            query.Limit,
            query.Offset);

        // Verify game exists and is published (public endpoint)
        var gameExists = await _context.SharedGames
            .AsNoTracking()
            .AnyAsync(
                g => g.Id == query.GameId && !g.IsDeleted && g.Status == (int)Domain.Entities.GameStatus.Published,
                cancellationToken)
            .ConfigureAwait(false);

        if (!gameExists)
        {
            _logger.LogWarning("Game {GameId} not found or not published", query.GameId);
            // Return empty result for non-existent/unpublished games (public endpoint behavior)
            return new GetGameFaqsResultDto([], 0);
        }

        // Get total count for pagination
        var totalCount = await _context.GameFaqs
            .AsNoTracking()
            .CountAsync(f => f.SharedGameId == query.GameId, cancellationToken)
            .ConfigureAwait(false);

        // Get paginated FAQs ordered by DisplayOrder then by UpvoteCount (descending)
        var faqs = await _context.GameFaqs
            .AsNoTracking()
            .Where(f => f.SharedGameId == query.GameId)
            .OrderBy(f => f.DisplayOrder)
            .ThenByDescending(f => f.UpvoteCount)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} FAQs (total: {Total}) for game {GameId}",
            faqs.Count,
            totalCount,
            query.GameId);

        // Map to DTOs
        var faqDtos = faqs
            .Select(f => new GameFaqDto(
                f.Id,
                f.SharedGameId,
                f.Question,
                f.Answer,
                f.DisplayOrder,
                f.UpvoteCount,
                f.CreatedAt,
                f.UpdatedAt))
            .ToList();

        return new GetGameFaqsResultDto(faqDtos, totalCount);
    }
}
