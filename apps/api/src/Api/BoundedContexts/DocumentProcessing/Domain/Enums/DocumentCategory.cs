namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Categorizes PDF documents by content type for pipeline routing.
/// Issue #5443: Only Rulebook, Expansion, and Errata enter the RulebookAnalysis pipeline.
/// </summary>
public enum DocumentCategory
{
    /// <summary>
    /// Full game rulebook. Enters RulebookAnalysis pipeline.
    /// </summary>
    Rulebook = 0,

    /// <summary>
    /// Expansion rulebook. Enters RulebookAnalysis pipeline with base game context.
    /// </summary>
    Expansion = 1,

    /// <summary>
    /// Errata/FAQ corrections. Enters RulebookAnalysis pipeline, produces diff against base.
    /// </summary>
    Errata = 2,

    /// <summary>
    /// Quick start guide (abbreviated rules). Vector search only, no structured analysis.
    /// </summary>
    QuickStart = 3,

    /// <summary>
    /// Reference card or cheat sheet. Vector search only, no structured analysis.
    /// </summary>
    Reference = 4,

    /// <summary>
    /// Player aid or setup guide. Vector search only, no structured analysis.
    /// </summary>
    PlayerAid = 5,

    /// <summary>
    /// Other document type. Vector search only, no structured analysis.
    /// </summary>
    Other = 6
}

/// <summary>
/// Extension methods for DocumentCategory pipeline routing.
/// </summary>
public static class DocumentCategoryExtensions
{
    /// <summary>
    /// Returns true if this document category should enter the RulebookAnalysis pipeline.
    /// </summary>
    public static bool IsAnalyzable(this DocumentCategory category)
        => category is DocumentCategory.Rulebook
            or DocumentCategory.Expansion
            or DocumentCategory.Errata;
}
