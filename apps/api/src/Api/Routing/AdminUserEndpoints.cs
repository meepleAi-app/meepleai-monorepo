namespace Api.Routing;

/// <summary>
/// User management endpoints (Admin only).
/// Handles user CRUD operations, tier/level management, activity, invitations, and impersonation.
/// Delegates to focused sub-files in the Admin/ directory.
/// </summary>
internal static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        // CRUD, search, and detail
        AdminUserCrudEndpoints.Map(group);
        // Tier, level, badges, role history, role change
        AdminUserTierEndpoints.Map(group);
        // Activity, library stats, quick actions, bulk ops, impersonation
        AdminUserActivityEndpoints.Map(group);
        // ISSUE-124: Invitation management
        AdminInvitationEndpoints.Map(group);

        return group;
    }
}
