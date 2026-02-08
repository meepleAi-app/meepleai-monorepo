using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Strategy management (Issue #3811, Epic #3687)
/// </summary>
internal static class AdminStrategyEndpoints
{
    public static void MapAdminStrategyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/strategies");

        // Placeholder endpoints - will implement full CQRS after #3850 merges

        group.MapGet("/", () => Results.Ok(Array.Empty<object>()))
            .WithName("AdminGetStrategies")
            .WithTags("Strategy", "Admin");

        group.MapPost("/", () => Results.Created("/api/v1/admin/strategies/placeholder", new { id = Guid.NewGuid() }))
            .WithName("AdminCreateStrategy")
            .WithTags("Strategy", "Admin");

        group.MapGet("/{id:guid}", (Guid id) => Results.Ok(new { id }))
            .WithName("AdminGetStrategyById")
            .WithTags("Strategy", "Admin");

        group.MapPut("/{id:guid}", (Guid id) => Results.Ok(new { id }))
            .WithName("AdminUpdateStrategy")
            .WithTags("Strategy", "Admin");

        group.MapDelete("/{id:guid}", () => Results.NoContent())
            .WithName("AdminDeleteStrategy")
            .WithTags("Strategy", "Admin");
    }
}
