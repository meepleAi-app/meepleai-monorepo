namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// RAG Enhancement: Graph RAG entity extraction interface.
/// Extracts knowledge graph triples (source--relation-->target) from game rulebook text
/// using LLM-based analysis.
/// </summary>
internal interface IEntityExtractor
{
    Task<EntityExtractionResult> ExtractEntitiesAsync(
        Guid gameId, string gameTitle, string textContent,
        CancellationToken ct);
}

internal sealed record EntityExtractionResult(
    List<ExtractedRelation> Relations, int TotalEntities);

internal sealed record ExtractedRelation(
    string SourceEntity, string SourceType,
    string Relation, string TargetEntity, string TargetType,
    float Confidence);
