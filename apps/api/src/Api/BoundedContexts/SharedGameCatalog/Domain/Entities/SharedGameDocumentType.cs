namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Types of documents that can be associated with a shared game.
/// </summary>
public enum SharedGameDocumentType
{
    /// <summary>
    /// Official rulebook for the game.
    /// </summary>
    Rulebook = 0,

    /// <summary>
    /// Errata and corrections to the official rules.
    /// </summary>
    Errata = 1,

    /// <summary>
    /// User-created house rules and variants.
    /// </summary>
    Homerule = 2
}
