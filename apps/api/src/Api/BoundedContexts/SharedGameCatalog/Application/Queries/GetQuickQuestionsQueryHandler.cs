using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for retrieving quick questions for a game.
/// </summary>
internal sealed class GetQuickQuestionsQueryHandler
    : IQueryHandler<GetQuickQuestionsQuery, IReadOnlyCollection<QuickQuestionDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetQuickQuestionsQueryHandler> _logger;

    public GetQuickQuestionsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetQuickQuestionsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyCollection<QuickQuestionDto>> Handle(
        GetQuickQuestionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving quick questions for game: {SharedGameId}, ActiveOnly: {ActiveOnly}",
            query.SharedGameId,
            query.ActiveOnly);

        // Verify game exists
        var gameExists = await _context.SharedGames
            .AsNoTracking()
            .AnyAsync(g => g.Id == query.SharedGameId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (!gameExists)
        {
            throw new InvalidOperationException($"Shared game with ID {query.SharedGameId} not found");
        }

        // Query questions from context
        var questionsQuery = _context.QuickQuestions
            .AsNoTracking()
            .Where(q => q.SharedGameId == query.SharedGameId);

        // Filter by active status if requested
        if (query.ActiveOnly)
        {
            questionsQuery = questionsQuery.Where(q => q.IsActive);
        }

        // Order by display order
        var questions = await questionsQuery
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} quick questions for game {SharedGameId}",
            questions.Count,
            query.SharedGameId);

        // Map to DTOs
        return questions
            .Select(q => new QuickQuestionDto(
                q.Id,
                q.SharedGameId,
                q.Text,
                q.Emoji,
                q.Category,
                q.DisplayOrder,
                q.IsGenerated,
                q.CreatedAt,
                q.IsActive))
            .ToList();
    }
}