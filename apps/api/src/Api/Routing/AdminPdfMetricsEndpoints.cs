using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for PDF processing metrics.
/// Issue #4212: Historical metrics and ETA calculation.
/// </summary>
internal static class AdminPdfMetricsEndpoints
{
    public static void MapAdminPdfMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/pdfs/metrics")
            .WithTags("Admin - PDF Metrics")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/pdfs/metrics/processing
        // Get aggregated processing metrics
        group.MapGet("/processing", GetProcessingMetrics)
            .WithName("GetProcessingMetrics")
            .WithSummary("Get aggregated PDF processing metrics")
            .WithDescription(@"
Returns aggregated metrics for PDF processing performance including:
- Average duration per processing step
- Percentile statistics (P50, P95, P99)
- Sample sizes for statistical confidence
- Last updated timestamp

**Use Cases:**
- Performance monitoring and optimization
- Capacity planning and resource allocation
- SLA compliance tracking
- Historical trend analysis

**Admin Only**: Requires admin authentication.
");
    }

    private static async Task<IResult> GetProcessingMetrics(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetProcessingMetricsQuery();
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
