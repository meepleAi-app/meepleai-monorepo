using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for PDF storage health monitoring.
/// PDF Storage Management Hub: Phase 2 (storage dashboard).
/// </summary>
internal static class AdminPdfStorageEndpoints
{
    public static void MapAdminPdfStorageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/pdfs/storage")
            .WithTags("Admin - PDF Storage")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/health", GetStorageHealth)
            .WithName("GetPdfStorageHealth")
            .WithSummary("Get PDF storage health across PG, Qdrant, and file storage");
    }

    private static async Task<IResult> GetStorageHealth(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPdfStorageHealthQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }
}
