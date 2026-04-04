using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetDocumentOverview;

/// <summary>
/// Query to get a consolidated document overview for a shared game.
/// Returns processing status breakdown, per-document details, agent linkage, and RAG readiness.
/// Issue #119: Per-SharedGame Document Overview.
/// </summary>
internal record GetDocumentOverviewQuery(Guid SharedGameId) : IQuery<DocumentOverviewResult>;

/// <summary>
/// Consolidated document overview for a shared game.
/// </summary>
internal record DocumentOverviewResult(
    Guid SharedGameId,
    string GameTitle,
    int TotalDocuments,
    StatusBreakdownDto StatusBreakdown,
    IReadOnlyList<DocumentOverviewItemDto> Documents,
    AgentStatusDto? AgentStatus,
    RagReadinessDto RagReadiness
);

/// <summary>
/// Processing state counts across all documents.
/// </summary>
internal record StatusBreakdownDto(
    int Ready,
    int Processing,
    int Failed,
    int Pending
);

/// <summary>
/// Per-document details including processing state and chunk metrics.
/// </summary>
internal record DocumentOverviewItemDto(
    Guid PdfDocumentId,
    string FileName,
    string? DocumentCategory,
    string? DocumentType,
    string? VersionLabel,
    string ProcessingState,
    string? CurrentStep,
    DateTime UploadedAt,
    int? ChunkCount,
    bool IsActiveForRag
);

/// <summary>
/// Linked agent status for the shared game.
/// </summary>
internal record AgentStatusDto(
    bool HasLinkedAgent,
    Guid? AgentId,
    string? AgentName,
    bool IsActive,
    DateTime? LastInvokedAt
);

/// <summary>
/// RAG pipeline readiness assessment.
/// </summary>
internal record RagReadinessDto(
    bool IsReady,
    int ReadyDocumentCount,
    int TotalChunks,
    IReadOnlyList<string> Blockers
);
