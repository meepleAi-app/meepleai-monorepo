namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Identifies how a <c>shared_game_translations</c> row was produced.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
public enum TranslationSource
{
    /// <summary>Default: admin-curated translation.</summary>
    Manual = 0,

    /// <summary>Auto-generated via OpenRouter translation service.</summary>
    AutoOpenRouter = 1,

    /// <summary>Community-sourced (future).</summary>
    Community = 2
}
