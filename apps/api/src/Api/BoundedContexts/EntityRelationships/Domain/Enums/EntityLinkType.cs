namespace Api.BoundedContexts.EntityRelationships.Domain.Enums;

/// <summary>
/// Defines the semantic type of a relationship between two entities.
/// </summary>
public enum EntityLinkType
{
    /// <summary>Source is an expansion of the target game.</summary>
    ExpansionOf = 1,

    /// <summary>Source is a sequel of the target game.</summary>
    SequelOf = 2,

    /// <summary>Source reimplements the target game.</summary>
    Reimplements = 3,

    /// <summary>Source pairs well with the target (bilateral).</summary>
    CompanionTo = 4,

    /// <summary>Generic relationship (bilateral).</summary>
    RelatedTo = 5,

    /// <summary>Source is part of the target event/collection (directed).</summary>
    PartOf = 6,

    /// <summary>Source collaborates with the target agent (bilateral).</summary>
    CollaboratesWith = 7,

    /// <summary>Source is specialized by the target agent (directed).</summary>
    SpecializedBy = 8
}
