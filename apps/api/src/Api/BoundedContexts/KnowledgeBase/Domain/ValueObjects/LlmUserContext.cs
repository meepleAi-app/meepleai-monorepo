using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Encapsulates the user-relevant context needed for LLM operations
/// within the KnowledgeBase domain. Decouples KB Domain from Authentication BC.
/// </summary>
/// <param name="UserId">User unique identifier (Guid), or null for anonymous users.</param>
/// <param name="RoleName">User role name (e.g. "admin", "editor"), or null for anonymous users.</param>
/// <param name="Tier">LLM routing tier derived from the user's role and subscription.</param>
public sealed record LlmUserContext(
    Guid? UserId,
    string? RoleName,
    LlmUserTier Tier)
{
    /// <summary>
    /// Represents an anonymous (unauthenticated) user context.
    /// </summary>
    public static LlmUserContext Anonymous => new(null, null, LlmUserTier.Anonymous);

    /// <summary>
    /// Represents an internal pipeline context (e.g. query rewriting).
    /// Uses User tier so internal services can access basic strategies.
    /// </summary>
    public static LlmUserContext Internal => new(null, "internal", LlmUserTier.User);

    /// <inheritdoc/>
    public override string ToString() => UserId?.ToString() ?? "anonymous";
}
