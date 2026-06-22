using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>A retrieved source chunk pinned for the analysis run (T2/T3/T4 source pool).</summary>
public sealed record MechanicSourceChunk(int ChunkIndex, int? PageNumber, Guid? ChunkId, string Content);

/// <summary>Everything a guardrail needs to evaluate one section output.</summary>
public sealed record MechanicGuardrailContext(
    MechanicSection Section,
    JsonElement Root,
    IReadOnlyList<MechanicSourceChunk> SourceChunks,
    int? PdfPageCount,
    MechanicGuardrailOptions Options)
{
    /// <summary>Analysis id, for structured logging (AC-7). Optional for unit tests.</summary>
    public Guid AnalysisId { get; init; } = Guid.Empty;

    /// <summary>Current retry attempt, for structured logging (AC-7).</summary>
    public int RetryCount { get; init; }
}

/// <summary>
/// One ADR-051 guardrail (T1 quote cap, T2 long-verbatim, T3 citation present/grounded,
/// T4 page+substring). Returns an empty list when the output passes.
/// </summary>
public interface IMechanicGuardrail
{
    /// <summary>Stable rule family prefix, e.g. "T1". Used for fail-fast ordering + metrics.</summary>
    string RuleFamily { get; }

    /// <summary>Lower runs first (cheapest-first). T1=10, T3a=15, T4=20, T2=30, T3b=40.</summary>
    int Order { get; }

    Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken);
}
