using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Business and Usage Statistics.
/// Issue #4562: App Usage Stats API (Epic #3688)
/// </summary>
internal static class AdminBusinessStatsEndpoints
{
    public static void MapAdminBusinessStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/business")
            .WithTags("Admin - Business Stats")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/business/usage
        group.MapGet("/usage", GetAppUsageStats)
            .WithName("GetAppUsageStats")
            .WithSummary("Get application usage statistics")
            .WithDescription(@"
Returns comprehensive application usage statistics including:
- DAU/MAU (Daily/Monthly Active Users) with trend comparison
- Session analytics (average duration, bounce rate)
- User retention cohorts (7/30/90 day)
- Feature adoption funnel (SignUp → FirstGame → FirstPDF)
- Geographic distribution of users

**Query Parameters:**
- period: Time period in days for analysis (default: 30, options: 7/30/90)

**Performance:**
- Cached for 15 minutes
- Target response time: <500ms (P95)

**Authorization:** Admin or SuperAdmin only
");
    }

    private static async Task<IResult> GetAppUsageStats(
        [FromQuery] int period,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Validate period parameter
        if (period is not (7 or 30 or 90))
        {
            period = 30; // Default to 30 days
        }

        var query = new GetAppUsageStatsQuery(period);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }
}
