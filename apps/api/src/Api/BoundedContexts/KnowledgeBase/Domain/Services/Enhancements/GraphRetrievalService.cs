using System.Globalization;
using System.Text;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// RAG Enhancement: Graph RAG retrieval service implementation.
/// Queries the game_entity_relations table for knowledge graph triples
/// and formats them as contextual information for the RAG prompt.
/// </summary>
internal sealed class GraphRetrievalService : IGraphRetrievalService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GraphRetrievalService> _logger;

    public GraphRetrievalService(
        MeepleAiDbContext db,
        ILogger<GraphRetrievalService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetEntityContextAsync(
        Guid gameId, int maxRelations, CancellationToken ct)
    {
        var relations = await _db.GameEntityRelations
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.Confidence)
            .Take(maxRelations)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (relations.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("[Knowledge Graph]");

        foreach (var r in relations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- {r.SourceEntity} ({r.SourceType}) --{r.Relation}--> {r.TargetEntity} ({r.TargetType})");
        }

        _logger.LogDebug("Graph retrieval: {Count} relations for game {GameId}",
            relations.Count, gameId);

        return sb.ToString().TrimEnd();
    }
}
