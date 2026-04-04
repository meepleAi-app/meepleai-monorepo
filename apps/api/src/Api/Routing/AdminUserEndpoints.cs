using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Routing.Admin;

#pragma warning disable MA0048 // File name must match type name - Endpoints and DTOs in same file
namespace Api.Routing;

/// <summary>
/// User management endpoints (Admin only).
/// Handles user CRUD operations and user search functionality.
/// Orchestrator: delegates to focused sub-files in Admin/.
/// </summary>
internal static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        // ADMIN-01: User search + CRUD
        AdminUserSearchCrudEndpoints.MapSearchCrudEndpoints(group);
        // ADMIN-TIER-01: Tier, level, library stats, badges
        AdminUserTierLevelBadgesEndpoints.MapTierLevelBadgesEndpoints(group);
        // BULK OPERATIONS - Issue #905 + account lockout #3339
        AdminUserBulkEndpoints.MapBulkEndpoints(group);
        // Activity, detail, role history, quick actions, role change, impersonation - Issue #911, #2890, #3349
        AdminUserActivityDetailEndpoints.MapActivityDetailEndpoints(group);
        // ISSUE-124: Invitation endpoints
        AdminUserInvitationEndpoints.MapInvitationEndpoints(group);

        return group;
    }
}

/// <summary>
/// Request payload for ending impersonation (Issue #3349).
/// </summary>
internal record EndImpersonationRequest(Guid SessionId);

/// <summary>
/// Response for end impersonation action (Issue #3349).
/// </summary>
internal record EndImpersonationResponse(bool Success, string Message);

/// <summary>
/// Request payload for updating user tier.
/// </summary>
internal record UpdateUserTierRequest(string Tier);

/// <summary>
/// Request payload for setting user level.
/// </summary>
internal record SetUserLevelRequest(int Level);

/// <summary>
/// Request payload for bulk password reset.
/// </summary>
internal record BulkPasswordResetRequest(IReadOnlyList<Guid> UserIds, string NewPassword);

/// <summary>
/// Request payload for bulk role change.
/// </summary>
internal record BulkRoleChangeRequest(IReadOnlyList<Guid> UserIds, string NewRole);

/// <summary>
/// Request payload for suspending a user account.
/// </summary>
internal record SuspendUserRequest(string? Reason);

/// <summary>
/// Request payload for resetting user password (Issue #2890).
/// </summary>
internal record ResetUserPasswordRequest(string NewPassword);

/// <summary>
/// Request payload for sending email to user (Issue #2890).
/// </summary>
internal record SendUserEmailRequest(string Subject, string Body);

/// <summary>
/// Request payload for sending an invitation (Issue #124).
/// </summary>
internal record SendInvitationRequest(string Email, string Role);

/// <summary>
/// Request payload for provisioning and inviting a user (Issue #124).
/// </summary>
internal record ProvisionAndInviteRequest(
    string Email,
    string DisplayName,
    string Role,
    string? Tier = "free",
    string? CustomMessage = null,
    int? ExpiresInDays = 7,
    List<GameSuggestionDto>? GameSuggestions = null);

/// <summary>
/// Request payload for changing a user's role (Issue #124).
/// </summary>
internal record ChangeUserRoleRequest(string NewRole, string? Reason = null);

/// <summary>
/// Request payload for creating a user (ADMIN-01).
/// </summary>
internal record CreateUserRequest(string Email, string Password, string DisplayName, string? Role = null);

/// <summary>
/// Request payload for updating a user (ADMIN-01).
/// </summary>
internal record UpdateUserRequest(string? Email, string? DisplayName, string? Role);
