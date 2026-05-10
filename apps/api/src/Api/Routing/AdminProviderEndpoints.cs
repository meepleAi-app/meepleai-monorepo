using System.Security.Claims;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
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
                HttpRequest request,
                CancellationToken ct) =>
            {
                var actorIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(actorIdClaim, out var actorId))
                    return Results.Unauthorized();

                // Optional ?model=X query parameter to also verify availability of a specific model.
                var expectedModel = request.Query.TryGetValue("model", out var modelValues)
                    ? modelValues.ToString()
                    : null;
                if (string.IsNullOrWhiteSpace(expectedModel))
                    expectedModel = null;

                try
                {
                    var result = await mediator.Send(new ProbeProviderCommand(name, actorId, expectedModel), ct).ConfigureAwait(false);
                    return Results.Ok(result);
                }
                catch (UnknownProviderException ex)
                {
                    return Results.NotFound(new { errorCode = "unknown_provider", provider = ex.ProviderName });
                }
            })
            // NOTE: only one .RequireRateLimiting() takes effect (the last call wins);
            // we apply the per-user policy (10/min). Global per-provider cap (60/h) is
            // available as the named policy "AdminProviderProbeGlobal" if needed via a
            // ChainedRateLimiter in the future.
            .RequireAuthorization("RequireSuperAdmin")
            .RequireRateLimiting("AdminProviderProbe")
            .WithOpenApi();

        // GET /api/v1/admin/providers/{name}/quota — Issue #936 (G2). Admin-or-above.
        // Cache 5min server-side. 200 with QuotaSupported:false for unknown / unsupported providers.
        group.MapGet("/{name}/quota", async (string name, IMediator mediator, CancellationToken ct) =>
            {
                var dto = await mediator.Send(new GetProviderQuotaQuery(name), ct).ConfigureAwait(false);
                return Results.Ok(dto);
            })
            .RequireAuthorization("RequireAdminOrAbove")
            .WithOpenApi();
    }
}
