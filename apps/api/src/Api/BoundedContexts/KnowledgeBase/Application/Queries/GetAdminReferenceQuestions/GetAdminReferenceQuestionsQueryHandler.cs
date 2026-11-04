using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminReferenceQuestions;

/// <summary>
/// #4470 (ADR-095 fetta 7b, D8): le domande di riferimento con il titolo del loro gioco.
/// </summary>
internal sealed class GetAdminReferenceQuestionsQueryHandler
    : IQueryHandler<GetAdminReferenceQuestionsQuery, AdminReferenceQuestionsDto>
{
    private readonly IReferenceQuestionRepository _questions;
    private readonly MeepleAiDbContext _db;

    public GetAdminReferenceQuestionsQueryHandler(IReferenceQuestionRepository questions, MeepleAiDbContext db)
    {
        _questions = questions ?? throw new ArgumentNullException(nameof(questions));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AdminReferenceQuestionsDto> Handle(
        GetAdminReferenceQuestionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var questions = await _questions.ListAsync(query.GameId, query.InGate, cancellationToken).ConfigureAwait(false);
        var countByGame = await _questions.CountByGameAsync(cancellationToken).ConfigureAwait(false);
        var titles = await GameTitlesAsync(countByGame.Keys.ToList(), cancellationToken).ConfigureAwait(false);

        var items = questions
            .Select(q => new AdminReferenceQuestionDto(
                q.Code,
                q.SharedGameId,
                titles.GetValueOrDefault(q.SharedGameId),
                q.Question,
                q.ExpectedAnswer,
                q.Category,
                q.Difficulty,
                q.QuestionType,
                q.SourceReference,
                q.OutOfCorpus,
                q.InGate))
            .ToList();

        var games = countByGame
            .Select(g => new AdminReferenceQuestionGameDto(g.Key, titles.GetValueOrDefault(g.Key), g.Value))
            .OrderBy(g => g.GameName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(g => g.GameId)
            .ToList();

        return new AdminReferenceQuestionsDto(items, games);
    }

    /// <summary>
    /// Il titolo anche dei giochi cancellati in modo logico: il seeder non li risolve più, ma le domande già
    /// caricate restano legate a loro, e l'admin deve poter capire di quale gioco sono.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GameTitlesAsync(
        IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken)
    {
        if (gameIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _db.SharedGames
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title })
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
            .ConfigureAwait(false);
    }
}
