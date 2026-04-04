namespace Api.Routing;

/// <summary>
/// PDF management routing orchestrator — delegates to focused sub-files.
/// Handles PDF upload, retrieval, deletion, indexing, and rule spec generation.
/// </summary>
internal static class PdfEndpoints
{
    public static RouteGroupBuilder MapPdfEndpoints(this RouteGroupBuilder group)
    {
        PdfUploadEndpoints.Map(group);     // Standard upload, private upload, chunked upload, BGG extraction
        PdfRetrievalEndpoints.Map(group);  // Retrieval, lifecycle, admin list
        PdfProcessingEndpoints.Map(group); // Processing state, processing actions

        return group;
    }
}
