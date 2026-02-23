namespace Api.BoundedContexts.EntityRelationships.Domain.Constants;

/// <summary>
/// Domain constants for the EntityRelationships bounded context.
/// </summary>
public static class EntityRelationshipsConstants
{
    /// <summary>Maximum length for the optional notes on an EntityLink (legacy).</summary>
    public const int NotesMaxLength = 500;

    /// <summary>Maximum length for the JSONB metadata field on an EntityLink.</summary>
    public const int MetadataMaxLength = 2000;

    /// <summary>Database table name for entity links.</summary>
    public const string EntityLinksTableName = "entity_links";
}
