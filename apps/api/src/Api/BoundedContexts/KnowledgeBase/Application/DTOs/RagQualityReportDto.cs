namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record RagQualityReportDto(
    int TotalIndexedDocuments,
    int TotalRaptorSummaries,
    int TotalEntityRelations,
    int TotalEmbeddedChunks,
    List<RagQualityGameBreakdown> TopGamesByChunkCount,
    List<RagEnhancementStatusDto> EnhancementStatuses);

public sealed record RagQualityGameBreakdown(
    Guid GameId,
    string GameTitle,
    int ChunkCount,
    int RaptorNodeCount,
    int EntityRelationCount);

public sealed record RagEnhancementStatusDto(
    string Name,
    string FeatureFlagKey,
    bool FreeEnabled,
    bool NormalEnabled,
    bool PremiumEnabled);
