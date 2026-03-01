namespace Api.BoundedContexts.EntityRelationships.Domain.Enums;

/// <summary>
/// Extension methods for <see cref="EntityLinkType"/>.
/// Issue #5131 (A2): IsBidirectional extension method.
/// </summary>
public static class EntityLinkTypeExtensions
{
    /// <summary>
    /// Returns true if the link type implies a bidirectional (symmetric) relationship.
    /// Bilateral types: CompanionTo, RelatedTo, CollaboratesWith.
    /// Directed types: ExpansionOf, SequelOf, Reimplements, PartOf, SpecializedBy.
    /// </summary>
    public static bool IsBidirectional(this EntityLinkType linkType) =>
        linkType is EntityLinkType.CompanionTo
            or EntityLinkType.RelatedTo
            or EntityLinkType.CollaboratesWith;

    /// <summary>
    /// Returns the snake_case string representation used in the database.
    /// </summary>
    public static string ToSnakeCase(this EntityLinkType linkType) => linkType switch
    {
        EntityLinkType.ExpansionOf => "expansion_of",
        EntityLinkType.SequelOf => "sequel_of",
        EntityLinkType.Reimplements => "reimplements",
        EntityLinkType.CompanionTo => "companion_to",
        EntityLinkType.RelatedTo => "related_to",
        EntityLinkType.PartOf => "part_of",
        EntityLinkType.CollaboratesWith => "collaborates_with",
        EntityLinkType.SpecializedBy => "specialized_by",
        _ => throw new ArgumentOutOfRangeException(nameof(linkType), linkType, "Unknown link type.")
    };
}
