using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// PDF Analytics endpoints for admin dashboard.
/// Issue #3715: PDF processing metrics - total uploaded, success/failure rate,
/// avg processing time, storage breakdown.
/// </summary>
internal static class PdfAnalyticsEndpoints
{
    internal static void MapPdfAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/pdf-analytics")
            .WithTags("Admin - PDF Analytics")
            .AddEndpointFilter<Filters.RequireAdminSessionFilter>();

        group.MapGet("/", HandleGetPdfAnalytics)
            .WithName("GetPdfAnalytics")
            .WithSummary("Get PDF processing analytics (Admin only)")
            .WithDescription(
                "Returns aggregated PDF processing metrics including total uploads, " +
                "success/failure rates, average processing times, storage breakdown, " +
                "and daily upload trends. Supports time range filtering. (Issue #3715)")
            .Produces<PdfAnalyticsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleGetPdfAnalytics(
        HttpContext context,
        IMediator mediator,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userRole = session.User?.Role ?? "Admin";

        var query = new GetPdfAnalyticsQuery(
            TimeRangeDays: Math.Clamp(days, 1, 365),
            UserId: null,
            UserRole: userRole);

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
