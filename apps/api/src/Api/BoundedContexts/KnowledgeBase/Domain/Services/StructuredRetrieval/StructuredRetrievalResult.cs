namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// A single result from structured retrieval against RulebookAnalysis fields.
/// Issue #5453: Structured RAG fusion.
/// </summary>
public sealed record StructuredRetrievalResult(
    string Content,
    double Confidence,
    StructuredQueryIntent SourceIntent,
    string SourceField,
    Guid SharedGameId);

/// <summary>
/// Aggregated result from the structured retrieval service including fusion metadata.
/// Issue #5453: Structured RAG fusion.
/// </summary>
public sealed record StructuredRetrievalResponse(
    IReadOnlyList<StructuredRetrievalResult> Results,
    QueryIntentClassification Classification,
    bool ShouldBypassVector,
    double StructuredContributionPercent);
