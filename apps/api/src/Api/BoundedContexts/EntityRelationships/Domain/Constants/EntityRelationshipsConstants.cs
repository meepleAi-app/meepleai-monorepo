namespace Api.BoundedContexts.EntityRelationships.Domain.Constants;

/// <summary>
/// Domain constants for the EntityRelationships bounded context.
/// </summary>
public static class EntityRelationshipsConstants
{
    /// <summary>Maximum length for the optional note on an EntityLink.</summary>
    public const int NotesMaxLength = 500;

    /// <summary>Database table name for entity links.</summary>
    public const string EntityLinksTableName = "entity_links";

    /// <summary>
    /// Link types that are bidirectional (both entities equally related).
    /// </summary>
    public static readonly IReadOnlySet<string> BilateralLinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "companion_to",
        "related_to",
        "collaborates_with"
    };
}
