namespace Api.BoundedContexts.SharedGameCatalog.Application.Configuration;

/// <summary>
/// Configuration options for the job that rejects mechanic analyses left in <c>Draft</c> by a pipeline that never finished.
/// Issue #4503: the pipeline runs in memory (<c>BackgroundTaskService</c>), so a restart or a crash loses it, and nothing
/// moves the row out of <c>Draft</c>. The filtered unique index <c>ux_mechanic_analyses_shared_game_pdf_prompt</c> counts a
/// <c>Draft</c>, so the orphan blocks its (game, PDF, prompt) slot for good.
/// </summary>
public sealed class MechanicAnalysisStaleDraftOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "BackgroundJobs:MechanicAnalysisStaleDraft";

    /// <summary>
    /// Default age after which a <c>Draft</c> is presumed orphaned. The pipeline has no time limit of its own (only a cost
    /// cap), so this is an estimate and not a measured bound. Nominal worst case, from the code: seven sections, run one
    /// after the other, each with up to <c>MaxRetriesPerSection + 1</c> = 3 attempts, each attempt capped by the provider
    /// HTTP client timeout (60 s for DeepSeek and OpenRouter, 120 s for Ollama): about 21 minutes, or 42 with Ollama.
    /// Two hours leaves a wide margin over that, but not over a retry layer below the clients, which this estimate does
    /// not count. A <c>Draft</c> whose pipeline is still alive when it is rejected loses the whole run (the final save is
    /// one transaction that finds the row already <c>Rejected</c>): raise the threshold if runs ever get that long.
    /// </summary>
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromHours(2);

    /// <summary>
    /// Drafts rejected per run. Not a setting: each one is its own write, and a backlog drains over a few runs
    /// (the job fires every 15 minutes) instead of holding one long pass.
    /// </summary>
    public const int BatchSize = 50;

    /// <summary>
    /// A <c>Draft</c> older than this is rejected. Zero or less is a misconfiguration, not "reject every draft, live ones
    /// included": the handler logs an error and touches nothing.
    /// </summary>
    public TimeSpan Threshold { get; set; } = DefaultThreshold;
}
