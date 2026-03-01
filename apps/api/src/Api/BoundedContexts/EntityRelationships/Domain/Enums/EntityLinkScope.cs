namespace Api.BoundedContexts.EntityRelationships.Domain.Enums;

/// <summary>
/// Defines the visibility scope of an EntityLink.
/// </summary>
public enum EntityLinkScope
{
    /// <summary>Visible only to the owner user. Auto-approved (BR-04).</summary>
    User = 1,

    /// <summary>Shared with all users. Requires admin approval (BR-05).</summary>
    Shared = 2
}
