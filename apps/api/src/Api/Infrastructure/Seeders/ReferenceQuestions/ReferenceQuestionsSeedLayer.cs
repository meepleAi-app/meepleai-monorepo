using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// #4470 (ADR-095 fetta 7b): carica il set delle domande di riferimento nella tabella
/// <c>knowledge_base.reference_questions</c>. Gira nei profili Staging e Dev, dopo
/// <c>CatalogSeedLayer</c>, che crea i giochi. Si salta con <c>SKIP_REFERENCE_QUESTIONS_SEED=true</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotente, per codice.</b> Una domanda nuova si inserisce, una cambiata si aggiorna, una uguale non
/// si tocca: due esecuzioni danno lo stesso stato. Una riga il cui codice non è più nel set <b>resta</b>,
/// con un avviso nel log: una valutazione passata può riferirsi a quel codice.
/// </para>
/// <para>
/// <b>Il gioco si risolve per titolo esatto</b> (<see cref="ReferenceQuestionGameResolver"/>). Un gioco
/// assente o ambiguo è un errore nel log che lo nomina, e le sue domande non si caricano; quelle degli altri
/// giochi sì.
/// </para>
/// <para>
/// <b>Non lancia.</b> L'orchestratore non isola le layer: un'eccezione qui fermerebbe quelle dopo (in Dev,
/// <c>LivedIn</c>). Le domande di riferimento servono alla valutazione, non devono bloccare il resto del
/// seed; ogni errore finisce nel log come errore.
/// </para>
/// </remarks>
internal sealed class ReferenceQuestionsSeedLayer : ISeedLayer
{
    internal const string SkipFlag = "SKIP_REFERENCE_QUESTIONS_SEED";

    private readonly Func<IReadOnlyList<ReferenceQuestionDefinition>> _loadSet;

    public ReferenceQuestionsSeedLayer()
        : this(ReferenceQuestionSet.LoadEmbedded)
    {
    }

    /// <summary>Per i test: un set diverso da quello incorporato.</summary>
    internal ReferenceQuestionsSeedLayer(Func<IReadOnlyList<ReferenceQuestionDefinition>> loadSet)
        => _loadSet = loadSet ?? throw new ArgumentNullException(nameof(loadSet));

    public string Name => "ReferenceQuestions";

    public SeedProfile MinimumProfile => SeedProfile.Staging;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = context.Services.GetService<IConfiguration>();
        if (config?.GetValue<bool>(SkipFlag) == true)
        {
            context.Logger.LogInformation("ReferenceQuestionsSeedLayer: {SkipFlag}=true, skipping", SkipFlag);
            return;
        }

        try
        {
            await SeedSetAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Logger.LogError(
                ex,
                "ReferenceQuestionsSeedLayer: the reference questions were not loaded; the other seed layers go on");
        }
    }

    private async Task SeedSetAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var db = context.DbContext;
        var logger = context.Logger;
        var set = _loadSet();

        var titles = set.Select(q => q.Game).Distinct(StringComparer.Ordinal).ToList();
        var catalog = await db.SharedGames
            .AsNoTracking()
            .Where(g => titles.Contains(g.Title))
            .Select(g => new CatalogGame(g.Id, g.Title))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var games = ReferenceQuestionGameResolver.Resolve(titles, catalog);
        LogUnresolvedGames(logger, set, games);

        // AsTracking: il contesto legge senza tracciamento per default (PERF-06), e un Update su una riga non
        // tracciata non arriverebbe al database.
        var existing = await db.ReferenceQuestions
            .AsTracking()
            .ToDictionaryAsync(q => q.Code, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        var now = (context.Services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        int inserted = 0, updated = 0, unchanged = 0, notLoaded = 0;
        foreach (var definition in set)
        {
            if (games[definition.Game] is not GameResolution.Found found)
            {
                notLoaded++;
                continue;
            }

            var content = new ReferenceQuestionContent(
                found.SharedGameId, definition.Question, definition.ExpectedAnswer, definition.Category,
                definition.Difficulty, definition.QuestionType, definition.SourceReference,
                definition.OutOfCorpus, definition.InGate);

            if (!existing.TryGetValue(definition.Code, out var row))
            {
                db.ReferenceQuestions.Add(ReferenceQuestion.Create(definition.Code, content, now));
                inserted++;
            }
            else if (row.Update(content, now))
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        var codesInSet = set.Select(q => q.Code).ToHashSet(StringComparer.Ordinal);
        var notInSet = existing.Keys
            .Where(code => !codesInSet.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
        if (notInSet.Count > 0)
        {
            logger.LogWarning(
                "ReferenceQuestionsSeedLayer: {Codes} no longer in the set; the rows are kept, because past evaluations may refer to them",
                string.Join(", ", notInSet));
        }

        if (inserted + updated > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "ReferenceQuestionsSeedLayer: {Inserted} inserted, {Updated} updated, {Unchanged} unchanged, {NotLoaded} not loaded (game not resolved)",
            inserted, updated, unchanged, notLoaded);
    }

    private static void LogUnresolvedGames(
        ILogger logger,
        IReadOnlyList<ReferenceQuestionDefinition> set,
        IReadOnlyDictionary<string, GameResolution> games)
    {
        foreach (var (title, resolution) in games)
        {
            var reason = resolution switch
            {
                GameResolution.Missing => "no catalog game has this exact title",
                GameResolution.Ambiguous ambiguous =>
                    $"{ambiguous.Candidates.Count} catalog games have this exact title ({string.Join(", ", ambiguous.Candidates)})",
                _ => null,
            };
            if (reason is null)
                continue;

            logger.LogError(
                "ReferenceQuestionsSeedLayer: game '{Game}' not resolved: {Reason}. Its reference questions are not loaded: {Codes}",
                title,
                reason,
                string.Join(", ", set.Where(q => string.Equals(q.Game, title, StringComparison.Ordinal)).Select(q => q.Code)));
        }
    }
}
