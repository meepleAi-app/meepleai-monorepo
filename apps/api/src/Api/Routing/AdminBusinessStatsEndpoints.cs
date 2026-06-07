using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Business and Usage Statistics.
/// Issue #4562: App Usage Stats API (Epic #3688)
/// Issue #1838: SP5 F4-C5 cost breakdown endpoints.
/// </summary>
internal static class AdminBusinessStatsEndpoints
{
    public static void MapAdminBusinessStatsEndpoints(this IEndpointRouteBuilder app)
    {
        // Group filter applies admin role check to every endpoint registered
        // below — mirrors the existing usage endpoint behaviour. New cost
        // breakdown endpoints (Issue #1838) inherit it.
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

        // GET /api/v1/admin/business/breakdown?range=30d  (Issue #1838 SP5 F4-C5)
        group.MapGet("/breakdown", GetCostBreakdownByProvider)
            .WithName("GetCostBreakdownByProvider")
            .WithSummary("Cost breakdown aggregated by provider + date for the stacked-area chart")
            .WithDescription(@"
Returns per-day cost buckets with a provider-level breakdown, used by the
admin CostStackedArea component.

**Query Parameters:**
- range: 7d | 30d | 90d | 1y (default: 30d)

**Performance:**
- HybridCache 5 min, tagged business:cost-breakdown + business:cost-breakdown:by-provider

**Authorization:** Admin or SuperAdmin only
");

        // GET /api/v1/admin/business/per-feature?range=30d  (Issue #1838 SP5 F4-C5)
        group.MapGet("/per-feature", GetCostBreakdownByFeature)
            .WithName("GetCostBreakdownByFeature")
            .WithSummary("Cost breakdown aggregated by feature (LedgerCategory) with provider drill")
            .WithDescription(@"
Returns total cost per feature within the range, plus a provider drill-down
so the FeatureCostTable can expand each row without a second round trip.

**Query Parameters:**
- range: 7d | 30d | 90d | 1y (default: 30d)

**Performance:**
- HybridCache 5 min, tagged business:cost-breakdown + business:cost-breakdown:by-feature

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

    private static async Task<IResult> GetCostBreakdownByProvider(
        [FromQuery] string? range,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parsedRange = CostBreakdownRangeExtensions.FromWireValue(range);
        var query = new GetCostBreakdownByProviderQuery(parsedRange);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCostBreakdownByFeature(
        [FromQuery] string? range,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var parsedRange = CostBreakdownRangeExtensions.FromWireValue(range);
        var query = new GetCostBreakdownByFeatureQuery(parsedRange);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
