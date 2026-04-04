namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Determines the level of content a user can access for RAG sources.
/// FullAccess: User owns the game — sees full text + images.
/// ReferenceOnly: User doesn't own the game — sees metadata (page, score) but not text/images.
/// NoAccess: Content blocked entirely (reserved for future use, e.g. DMCA takedown).
/// </summary>
public enum ContentAccessLevel
{
    /// <summary>User owns the game — full text and image access.</summary>
    FullAccess,

    /// <summary>User doesn't own the game — reference metadata only, no text/images.</summary>
    ReferenceOnly,

    /// <summary>Content blocked entirely (future use).</summary>
    NoAccess
}
