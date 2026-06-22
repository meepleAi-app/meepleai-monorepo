namespace Api.Configuration;

/// <summary>
/// Issue #562 — per-call LLM model override based on
/// <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier"/>.
///
/// Bound to <c>LlmRouting:QueryComplexityModels</c> in appsettings.json.
///
/// Semantics:
/// <list type="bullet">
///   <item>Each tier (<c>Low</c>/<c>Medium</c>/<c>High</c>) maps to an OPTIONAL model
///         identifier (provider-prefixed, e.g. <c>"openai/gpt-4o-mini"</c>).</item>
///   <item>When a tier's override is <c>null</c> or empty, the handler keeps using the
///         default model selected by the underlying <see cref="Api.Services.ILlmService"/>
///         (backward-compatible no-op).</item>
///   <item>When set, the handler dispatches through
///         <see cref="Api.Services.ILlmService.GenerateCompletionWithModelAsync"/>
///         using the configured override.</item>
/// </list>
///
/// Rationale: orthogonal to <see cref="Api.BoundedContexts.SystemConfiguration.Application.Services.ILlmTierRoutingService"/>
/// (Issue #2596) — that one routes by USER tier (Anonymous/User/Editor/Admin/Premium),
/// this one routes by QUERY complexity (Low/Medium/High). Both can coexist:
/// query-complexity override wins per-call when configured, user-tier remains the
/// fallback selection.
/// </summary>
internal sealed class LlmQueryComplexityRoutingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json (relative to LlmRouting).
    /// </summary>
    public const string SectionName = "LlmRouting:QueryComplexityModels";

    /// <summary>
    /// Model override for <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.Low"/>
    /// queries (short, factual lookups). Empty/null → default model.
    /// </summary>
    public string? Low { get; set; }

    /// <summary>
    /// Model override for <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.Medium"/>
    /// queries. Empty/null → default model.
    /// </summary>
    public string? Medium { get; set; }

    /// <summary>
    /// Model override for <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.High"/>
    /// queries (complex analysis, comparisons). Empty/null → default model.
    /// </summary>
    public string? High { get; set; }

    /// <summary>
    /// Resolves the override for a given tier. Returns <c>null</c> when the entry is
    /// missing or empty so the handler can dispatch to the default
    /// <see cref="Api.Services.ILlmService.GenerateCompletionAsync"/> path.
    /// </summary>
    public string? ResolveOverride(Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier tier)
    {
        var raw = tier switch
        {
            Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.Low => Low,
            Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.Medium => Medium,
            Api.BoundedContexts.KnowledgeBase.Application.Services.QueryRoutingTier.High => High,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }
}
