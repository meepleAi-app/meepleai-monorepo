namespace Api.Routing;

/// <summary>
/// UserLibrary routing orchestrator — delegates to focused sub-files in UserLibrary/.
/// </summary>
internal static class UserLibraryEndpoints
{
    public static RouteGroupBuilder MapUserLibraryEndpoints(this RouteGroupBuilder group)
    {
        UserLibraryCoreEndpoints.Map(group);
        UserLibraryPdfEndpoints.Map(group);
        UserLibraryLabelEndpoints.Map(group);
        UserLibraryCollectionEndpoints.Map(group);
        UserLibraryAgentEndpoints.Map(group);

        return group;
    }
}
