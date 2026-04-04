using Api.Filters;
using Api.Infrastructure;
using Api.Infrastructure.Seeders;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for manually triggering the seeding pipeline.
/// </summary>
internal static class AdminSeedingEndpoints
{
    public static void MapAdminSeedingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/seeding")
            .AddEndpointFilter<RequireAdminSessionFilter>()
            .WithTags("Admin - Seeding");

        group.MapPost("/orchestrate", async (
            SeedOrchestrator orchestrator,
            MeepleAiDbContext db,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            await orchestrator.RunAsync(db, services, ct).ConfigureAwait(false);
            return Results.Ok(new { message = "Seeding pipeline completed" });
        })
        .WithName("OrchestrateSeeding")
        .WithSummary("Run the seeding pipeline manually");
    }
}
