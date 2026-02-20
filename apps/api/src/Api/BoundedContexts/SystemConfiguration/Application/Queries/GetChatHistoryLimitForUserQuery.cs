using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Returns the configurable chat history limit for a specific user based on their tier and role.
/// Admin/Editor roles return 0 (unlimited).
/// Issue #4918: Integrates with sliding window archiving in Issue #4913.
/// </summary>
/// <param name="UserTier">User subscription tier (e.g. "free", "normal", "premium")</param>
/// <param name="UserRole">User role (e.g. "User", "Admin", "Editor")</param>
internal record GetChatHistoryLimitForUserQuery(
    string? UserTier,
    string? UserRole
) : IQuery<int>;
