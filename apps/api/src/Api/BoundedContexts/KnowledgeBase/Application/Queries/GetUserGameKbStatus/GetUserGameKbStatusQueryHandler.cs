using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

/// <summary>
/// Handles GetUserGameKbStatusQuery.
/// Returns the per-game KB indexing status, coverage score, and suggested questions for end users.
/// KB-03: User-facing KB status endpoint.
/// Issue #1529: Extended to aggregate chunk/embedding counts, last-reindex/RAPTOR rebuild
/// timestamps, and per-game cost rollups so the KB Hub presentational components shipped
/// by #1481 (PR #1528) can be wired without follow-up endpoints.
/// </summary>
/// <remarks>
/// Null-default policy (see <see cref="UserGameKbStatusDto"/> XML doc for the contract):
/// <list type="bullet">
///   <item><c>ChunksCount</c>/<c>EmbeddingsCount</c>: <c>0</c> when not yet indexed.</item>
///   <item><c>LastReindexAt</c>: <c>null</c> when no VectorDocument exists for the game.</item>
///   <item><c>RaptorLastRebuildAt</c>: <c>null</c> when RAPTOR tree has never been rebuilt.</item>
///   <item><c>LifetimeCostUsd</c>: <c>0.00m</c> placeholder — per-game cost attribution does
///     not yet exist (<c>LlmCostLogEntity</c> has no <c>GameId</c> column). Tracked as future BE
///     follow-up; field is exposed today so the FE Sparkline + cost card render the
///     "no data yet" state without a schema change later.</item>
///   <item><c>CostHistoryLast7Days</c>: empty <c>[]</c> (same root cause).</item>
/// </list>
/// Direct <see cref="MeepleAiDbContext"/> read access is consistent with sibling queries in
/// this BC (e.g. <c>GetGamesWithoutKbQueryHandler</c>, <c>GetDocumentEmbeddingsMetaQueryHandler</c>).
/// </remarks>
internal sealed class GetUserGameKbStatusQueryHandler
    : IQueryHandler<GetUserGameKbStatusQuery, UserGameKbStatusDto>
{
    private readonly IVectorDocumentRepository _vectorRepo;
    private readonly IConfigurationRepository _configRepo;
    private readonly MeepleAiDbContext _db;

    public GetUserGameKbStatusQueryHandler(
        IVectorDocumentRepository vectorRepo,
        IConfigurationRepository configRepo,
        MeepleAiDbContext db)
    {
        _vectorRepo = vectorRepo ?? throw new ArgumentNullException(nameof(vectorRepo));
        _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UserGameKbStatusDto> Handle(
        GetUserGameKbStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var documents = await _vectorRepo
            .GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var isIndexed = documents.Count > 0;
        var documentCount = documents.Count;

        int coverageScore = 0;
        string coverageLevel = "None";
        List<string> suggestedQuestions = [];

        if (isIndexed)
        {
            // Read coverage score
            string coverageKey = $"KB:Coverage:{query.GameId}";
            var coverageConfig = await _configRepo
                .GetByKeyAsync(coverageKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (coverageConfig is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(coverageConfig.Value);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("score", out var scoreEl))
                        coverageScore = scoreEl.GetInt32();
                    if (root.TryGetProperty("level", out var levelEl))
                        coverageLevel = levelEl.GetString() ?? "None";
                }
                catch
                {
                    // Swallow JSON parsing errors — defaults already set
                }
            }

            // Read suggested questions
            string questionsKey = $"KB:SuggestedQuestions:{query.GameId}";
            var questionsConfig = await _configRepo
                .GetByKeyAsync(questionsKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (questionsConfig is not null)
            {
                try
                {
                    var questions = JsonSerializer.Deserialize<List<string>>(questionsConfig.Value);
                    if (questions is not null)
                        suggestedQuestions = questions;
                }
                catch
                {
                    // Swallow JSON parsing errors — defaults already set
                }
            }
        }

        // ---- Issue #1529 deferred aggregates ----

        // Chunks: sum across VectorDocument rows for this game.
        // EmbeddingsCount == ChunksCount: each chunk has 1 row in the pgvector embeddings table
        // (pgvector is keyed 1:1 to TextChunk via the embeddings row). Diverging values would
        // signal a partial-failure ingestion and is out of scope for #1529.
        int chunksCount = documents.Sum(d => d.TotalChunks);
        int embeddingsCount = chunksCount;

        DateTime? lastReindexAt = isIndexed
            ? documents.Max(d => d.IndexedAt)
            : null;

        // RAPTOR: most-recent CreatedAt across all RaptorSummaryEntity rows for the game
        // (any tree level — leaf, section, overview). Null when never rebuilt.
        DateTime? raptorLastRebuildAt = await _db.RaptorSummaries
            .AsNoTracking()
            .Where(r => r.GameId == query.GameId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (DateTime?)r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Cost aggregates: per-game attribution does not exist yet (LlmCostLogEntity has no GameId).
        // Surfacing as constants now so the FE Sparkline + cost card render the "no data" state
        // without a schema change once attribution lands.
        decimal lifetimeCostUsd = 0.00m;
        IReadOnlyList<decimal> costHistoryLast7Days = Array.Empty<decimal>();

        return new UserGameKbStatusDto(
            GameId: query.GameId,
            IsIndexed: isIndexed,
            DocumentCount: documentCount,
            CoverageScore: coverageScore,
            CoverageLevel: coverageLevel,
            SuggestedQuestions: suggestedQuestions,
            ChunksCount: chunksCount,
            EmbeddingsCount: embeddingsCount,
            LastReindexAt: lastReindexAt,
            RaptorLastRebuildAt: raptorLastRebuildAt,
            LifetimeCostUsd: lifetimeCostUsd,
            CostHistoryLast7Days: costHistoryLast7Days);
    }
}
