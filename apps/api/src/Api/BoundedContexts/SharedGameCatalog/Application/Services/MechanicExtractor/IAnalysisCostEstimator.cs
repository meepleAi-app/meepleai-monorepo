using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Estimates the USD cost of a Mechanic Extractor pipeline run ahead of actual LLM calls.
/// Used by <c>GenerateMechanicAnalysisCommandHandler</c> to honour ADR-051 T8 cost caps:
/// if the projected cost exceeds <c>CostCapUsd × 1.0</c> and no runtime override is provided,
/// the command fails fast before any tokens are spent.
/// </summary>
/// <remarks>
/// ISSUE-524 / M1.2. The estimator intentionally operates on heuristics (average chunk token
/// counts, per-section output budgets) rather than a precise dry-run tokenization — the goal
/// is a conservative upper bound, not a prediction.
/// </remarks>
public interface IAnalysisCostEstimator
{
    AnalysisCostEstimate Estimate(AnalysisCostEstimateInput input);
}

/// <summary>
/// Input to cost projection. Caller supplies the planned sections and the retrieved chunk
/// inventory (token counts already known from the retrieval step).
/// </summary>
public sealed record AnalysisCostEstimateInput(
    string PromptVersion,
    string Provider,
    string Model,
    IReadOnlyCollection<MechanicSection> Sections,
    int TotalRetrievedPromptTokens,
    decimal InputCostPerMillionTokens,
    decimal OutputCostPerMillionTokens);

/// <summary>
/// Projected cost breakdown for a full pipeline run.
/// </summary>
public sealed record AnalysisCostEstimate(
    int ProjectedPromptTokens,
    int ProjectedCompletionTokens,
    int ProjectedTotalTokens,
    decimal ProjectedCostUsd,
    IReadOnlyDictionary<MechanicSection, SectionCostProjection> PerSection);

/// <summary>Per-section projection of tokens and cost.</summary>
public sealed record SectionCostProjection(
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd);
