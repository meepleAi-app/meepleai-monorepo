using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ChatThread aggregate.
/// </summary>
internal interface IChatThreadRepository : IRepository<ChatThread, Guid>
{
    /// <summary>
    /// Finds threads by user ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads by game ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads by user ID and game ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByUserIdAndGameIdAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds recent threads (ordered by last message).
    /// </summary>
    /// <summary>
    /// Counts total threads for a user (for pagination).
    /// </summary>
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatThread>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads for a user with filters and pagination (Issue #4362).
    /// </summary>
    Task<(IReadOnlyList<ChatThread> Items, int TotalCount)> FindByUserIdFilteredAsync(
        Guid userId,
        Guid? gameId = null,
        string? agentType = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight read-model for the admin chat history listing.
    /// Avoids full ChatThread domain hydration — only the fields needed for the table + preview.
    /// Issue #4917: Admin chat history real data.
    /// </summary>
    internal record AdminChatSummary(
        Guid Id,
        Guid UserId,
        string? AgentType,
        int MessageCount,
        DateTime LastMessageAt,
        DateTime CreatedAt,
        string? UserEmail,
        string? UserDisplayName,
        IReadOnlyList<(string Role, string Content)> PreviewMessages
    );

    /// <summary>
    /// Admin-only: gets all chat threads across users with lightweight projection (no full MapToDomain).
    /// Issue #4917: Admin chat history real data.
    /// </summary>
    Task<(IReadOnlyList<AdminChatSummary> Items, int TotalCount)> GetAllFilteredAsync(
        string? agentType = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
