namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence record for a single section generation run within a
/// <see cref="MechanicAnalysisEntity"/> (ADR-051 / M1.2, decision B6=C). Captures the provider,
/// model, token usage, cost, latency and final status of each per-section LLM call so operators
/// can audit token spend and diagnose provider failures without trawling logs.
/// </summary>
/// <remarks>
/// One row per section attempt — rerunning the pipeline creates a new analysis with its own
/// set of section runs. Rows are write-once: once a section completes, its row is immutable.
/// </remarks>
public class MechanicAnalysisSectionRunEntity
{
    public Guid Id { get; set; }

    public Guid AnalysisId { get; set; }

    /// <summary>
    /// 0=Summary, 1=Mechanics, 2=Victory, 3=Resources, 4=Phases, 5=Faq.
    /// Mirrors <c>Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicSection</c>.
    /// </summary>
    public int Section { get; set; }

    /// <summary>Order within the pipeline run (0-based).</summary>
    public int RunOrder { get; set; }

    /// <summary>Provider that actually produced the completion (after fallback): deepseek, openrouter.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Canonical model identifier returned by the provider.</summary>
    public string ModelUsed { get; set; } = string.Empty;

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Wall-clock duration of the LLM call in milliseconds.</summary>
    public int LatencyMs { get; set; }

    /// <summary>0=Succeeded, 1=Failed, 2=SkippedDueToCostCap.</summary>
    public int Status { get; set; }

    /// <summary>Free-text error message when <see cref="Status"/> is Failed; null otherwise.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when the run started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>UTC timestamp when the run ended (success or failure).</summary>
    public DateTime CompletedAt { get; set; }

    // === Navigation ===
    public MechanicAnalysisEntity Analysis { get; set; } = default!;
}
