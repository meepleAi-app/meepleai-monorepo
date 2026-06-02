namespace Api.BoundedContexts.KbQuality.Domain.Goldset;

/// <summary>
/// Outcome of a goldset generation run (Phase A of per-doc eval, #1675).
/// Aggregates the generated Q&amp;A pairs with run-level cost + duration telemetry
/// surfaced to the audit log + cost budget counters.
/// </summary>
public sealed record GoldsetGenerationResult(
    IReadOnlyList<GoldsetQaPair> Pairs,
    decimal CostUsd,
    TimeSpan Elapsed);

/// <summary>
/// A single Q&amp;A pair derived from a source chunk by the goldset generator.
/// <see cref="SourceChunkId"/> binds the pair to the originating
/// <see cref="Api.BoundedContexts.KbQuality.Application.Ports.ChunkSnapshot"/>,
/// allowing later metric calculations (recall@k, MRR) to score retrieval against
/// the expected chunk lineage.
/// </summary>
public sealed record GoldsetQaPair(
    string Id,
    string Question,
    string ExpectedAnswer,
    Guid SourceChunkId);
