using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Orchestrates the per-section LLM calls of a Mechanic Extractor run (ISSUE-524 / M1.2).
/// The pipeline is stateless — state is carried by the caller (handler) which holds the
/// <c>MechanicAnalysis</c> aggregate and the DbContext.
/// </summary>
/// <remarks>
/// <b>Failure semantics</b> (ADR-051):
/// <list type="bullet">
/// <item><description>If the <i>cumulative</i> cost after a section exceeds the effective cost cap, the pipeline returns <see cref="MechanicPipelineAbortReason.CostCapExceeded"/> mid-run.</description></item>
/// <item><description>If an LLM call hard-fails after provider fallback, the pipeline returns <see cref="MechanicPipelineAbortReason.LlmGenerationFailed"/>.</description></item>
/// <item><description>If validator rejects output after retries, returns <see cref="MechanicPipelineAbortReason.ValidationFailedBeyondRetry"/>.</description></item>
/// </list>
/// Each section attempt — success or failure — produces a <see cref="MechanicAnalysisSectionRunEntity"/>
/// row so operators can audit spend and diagnose provider behaviour (B6=C).
/// </remarks>
public interface IMechanicAnalysisPipeline
{
    Task<MechanicPipelineResult> RunAsync(MechanicPipelineRequest request, CancellationToken cancellationToken);
}

/// <summary>Input to a pipeline run.</summary>
public sealed record MechanicPipelineRequest(
    Guid AnalysisId,
    Guid SharedGameId,
    Guid PdfDocumentId,
    string PromptVersion,
    IReadOnlyList<MechanicSection> Sections,
    IReadOnlyDictionary<MechanicSection, string> RetrievedContextBySection,
    string Provider,
    string Model,
    decimal EffectiveCostCapUsd,
    decimal InputCostPerMillionTokens,
    decimal OutputCostPerMillionTokens);

/// <summary>Outcome of a pipeline run.</summary>
public sealed record MechanicPipelineResult(
    MechanicPipelineOutcome Outcome,
    IReadOnlyList<MechanicAnalysisSectionRunEntity> SectionRuns,
    IReadOnlyDictionary<MechanicSection, string> SectionOutputs,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalCostUsd,
    string? AbortDetail)
{
    public bool IsSuccess => Outcome == MechanicPipelineOutcome.Succeeded;

    public MechanicPipelineAbortReason? AbortReason => Outcome switch
    {
        MechanicPipelineOutcome.AbortedCostCap => MechanicPipelineAbortReason.CostCapExceeded,
        MechanicPipelineOutcome.AbortedLlmFailed => MechanicPipelineAbortReason.LlmGenerationFailed,
        MechanicPipelineOutcome.AbortedValidation => MechanicPipelineAbortReason.ValidationFailedBeyondRetry,
        _ => null
    };
}

public enum MechanicPipelineOutcome
{
    Succeeded = 0,
    AbortedCostCap = 1,
    AbortedLlmFailed = 2,
    AbortedValidation = 3
}

public enum MechanicPipelineAbortReason
{
    CostCapExceeded = 0,
    LlmGenerationFailed = 1,
    ValidationFailedBeyondRetry = 2
}
