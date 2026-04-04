using Api.BoundedContexts.Administration.Application.Queries.CircuitBreakers;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for reading in-memory Polly circuit breaker states across all external services.
/// </summary>
internal static class AdminCircuitBreakerEndpoints
{
    public static RouteGroupBuilder MapAdminCircuitBreakerEndpoints(this RouteGroupBuilder group)
    {
        var cbGroup = group.MapGroup("/admin/circuit-breakers").WithTags("Admin", "CircuitBreakers");

        cbGroup.MapGet("/", async (
            IMediator mediator, HttpContext httpContext, CancellationToken ct) =>
        {
            var (authorized, _, error) = httpContext.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetCircuitBreakerStatesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Get circuit breaker states for all external services");

        return group;
    }
}
