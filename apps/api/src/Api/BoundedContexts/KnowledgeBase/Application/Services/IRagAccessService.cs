using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Service for checking RAG access based on ownership/role cascading rules.
/// Ownership/RAG access feature: determines whether a user can access RAG-indexed
/// knowledge base cards for a given game.
/// </summary>
public interface IRagAccessService
{
    /// <summary>
    /// Checks whether a user can access RAG content for a specific game.
    /// Cascading rules:
    /// 1. Admin/SuperAdmin role → true
    /// 2. SharedGame.IsRagPublic == true → true
    /// 3. UserLibraryEntry.OwnershipDeclaredAt != null → true
    /// 4. Otherwise → false
    /// </summary>
    Task<bool> CanAccessRagAsync(Guid userId, Guid gameId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns VectorDocument IDs the user is allowed to access for a given game.
    /// Returns empty list if the user has no RAG access.
    /// </summary>
    Task<List<Guid>> GetAccessibleKbCardsAsync(Guid userId, Guid gameId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accessible KB card IDs, filtered by a user selection list.
    /// If selectedIds is null/empty, returns all accessible KBs (backward compatible).
    /// </summary>
    Task<List<Guid>> GetAccessibleKbCardsFilteredAsync(
        Guid userId, Guid gameId, UserRole role,
        List<Guid>? selectedIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the SharedGame IDs accessible to the given user for cross-game KB operations.
    /// RBAC rules:
    /// 1. Admin or SuperAdmin → all non-deleted SharedGame IDs.
    /// 2. Otherwise → union of:
    ///    - SharedGames where IsRagPublic == true and IsDeleted == false.
    ///    - SharedGames in the user's library where OwnershipDeclaredAt != null (EC-8).
    /// Result is deduplicated. Returns an empty list (EC-1) if no games are accessible.
    /// Issue #1661: RBAC foundation for cross-game search (PR-1) and SSE ask (PR-2).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAccessibleGameIdsAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default);
}
