using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Result for the per-query drill endpoint (#1728).
/// Contains the AI request log entity plus optional chunks list and
/// per-stage latency breakdown.
/// </summary>
internal record AiQueryDrillResult
{
    /// <summary>The AI request log entity (canonical row).</summary>
    public required AiRequestLogEntity Request { get; init; }

    /// <summary>
    /// Retrieved chunks for this query. Empty array when the pipeline
    /// did not record chunks (legacy rows, error paths, or endpoints
    /// without RAG retrieval).
    /// </summary>
    public required IReadOnlyList<RetrievedChunkDto> Chunks { get; init; }

    /// <summary>
    /// Per-stage latency breakdown. Null when not instrumented yet.
    /// FE renders "breakdown unavailable" fallback in that case.
    /// </summary>
    public LatencyBreakdownDto? Breakdown { get; init; }
}

/// <summary>
/// A single retrieved chunk surfaced by the drill endpoint.
/// Shape mirrors `apps/web/.../sandbox/types.ts` <c>RetrievedChunk</c>.
/// </summary>
internal record RetrievedChunkDto
{
    public required string Id { get; init; }
    public required double Score { get; init; }
    public required string Text { get; init; }
    public required int Page { get; init; }
    public required int ChunkIndex { get; init; }
    public required string PdfName { get; init; }
    public required bool Used { get; init; }
}

/// <summary>
/// Per-stage latency split for a single AI query.
/// Must surface ALL four fields together (no partial breakdown).
/// </summary>
internal record LatencyBreakdownDto
{
    public required int RetrievalMs { get; init; }
    public required int RerankMs { get; init; }
    public required int LlmMs { get; init; }
    public required int PostMs { get; init; }
}
