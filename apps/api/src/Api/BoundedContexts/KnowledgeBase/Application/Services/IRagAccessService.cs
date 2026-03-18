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
}
