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
/// Detailed guardrail result: the violations plus an optional numeric score (T3b only).
/// <paramref name="Score"/> is the section-wide min cosine (kept for telemetry); <paramref name="ClaimScores"/>
/// (#2811) carries the PER-CLAIM cosine keyed by the claim object's JSONPath so each claim can render
/// its own grounding score instead of the misleading section min. Null for non-T3b guardrails.
/// </summary>
public sealed record MechanicGuardrailResult(
    IReadOnlyList<MechanicValidationViolation> Violations,
    double? Score = null,
    IReadOnlyDictionary<string, double>? ClaimScores = null);

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

    /// <summary>
    /// Collect-all detailed evaluation (#2782 D1/D2). Default: wrap <see cref="EvaluateAsync"/>
    /// with a null score. Only <c>GroundingGuardrail</c> overrides to surface its cosine.
    /// </summary>
    async Task<MechanicGuardrailResult> EvaluateDetailedAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken)
    {
        var violations = await EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return new MechanicGuardrailResult(violations, Score: null);
    }
}
