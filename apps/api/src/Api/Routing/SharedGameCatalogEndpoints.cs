namespace Api.Routing;

/// <summary>
/// SharedGameCatalog routing orchestrator — delegates to focused sub-files.
/// Issue #2371 Phase 2
/// </summary>
internal static class SharedGameCatalogEndpoints
{
    public static RouteGroupBuilder MapSharedGameCatalogEndpoints(this RouteGroupBuilder group)
    {
        SharedGameCatalogPublicEndpoints.Map(group);
        SharedGameCatalogUserEndpoints.Map(group);
        SharedGameCatalogAdminEndpoints.Map(group);
        SharedGameCatalogAdminShareRequestEndpoints.Map(group);
        SharedGameCatalogUserShareRequestEndpoints.Map(group);
        SharedGameCatalogContributorEndpoints.Map(group);
        SharedGameCatalogBadgeEndpoints.Map(group);
        SharedGameCatalogTrendingEndpoints.Map(group);
        SharedGameCatalogWizardEndpoints.Map(group); // Issue #4139: PDF Wizard endpoints

        return group;
    }
}
