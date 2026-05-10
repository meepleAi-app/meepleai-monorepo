using System.Security.Claims;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for provider token observability. Issue #936 (G1+G3).
/// </summary>
internal static class AdminProviderEndpoints
{
    public static void MapAdminProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/providers")
                       .WithTags("Admin - Providers");

        group.MapPost("/{name}/probe", async (
                string name,
                IMediator mediator,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                var actorIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(actorIdClaim, out var actorId))
                    return Results.Unauthorized();

                try
                {
                    var result = await mediator.Send(new ProbeProviderCommand(name, actorId), ct);
                    return Results.Ok(result);
                }
                catch (UnknownProviderException ex)
                {
                    return Results.NotFound(new { errorCode = "unknown_provider", provider = ex.ProviderName });
                }
            })
            .RequireAuthorization("RequireSuperAdmin")
            .RequireRateLimiting("AdminProviderProbe")
            .RequireRateLimiting("AdminProviderProbeGlobal")
            .WithOpenApi();
    }
}
