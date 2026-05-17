using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Issue #1089: Endpoints for the global incident/status banner.
///   GET    /api/v1/status-banner            (public)
///   GET    /api/v1/admin/status-banner      (admin)
///   PUT    /api/v1/admin/status-banner      (admin)
/// </summary>
internal static class StatusBannerEndpoints
{
    public static IEndpointRouteBuilder MapStatusBannerEndpoints(this IEndpointRouteBuilder app)
    {
        // Public GET — returns 204 No Content when there is nothing to show.
        app.MapGet("/status-banner", async (
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var banner = await mediator.Send(new GetPublicStatusBannerQuery(), ct).ConfigureAwait(false);

            // Short cache so updates propagate within ~30s even via CDN.
            context.Response.Headers.CacheControl = "public, max-age=30, s-maxage=30";

            return banner is null
                ? Results.NoContent()
                : Results.Ok(banner);
        })
        .WithName("GetPublicStatusBanner")
        .WithTags("Status Banner")
        .Produces<PublicStatusBannerResponse>(200)
        .Produces(204)
        .AllowAnonymous();

        // Admin GET — full state including inactive.
        app.MapGet("/admin/status-banner", async (
            HttpContext context,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var banner = await mediator.Send(new GetAdminStatusBannerQuery(), ct).ConfigureAwait(false);
            return Results.Ok(banner);
        })
        .WithName("GetAdminStatusBanner")
        .WithTags("Admin", "Status Banner")
        .Produces<AdminStatusBannerResponse>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // Admin PUT — replace current state.
        app.MapPut("/admin/status-banner", async (
            [FromBody] UpdateStatusBannerRequest request,
            HttpContext context,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var updatedBy = session.User?.Id.ToString();

            var command = new UpdateStatusBannerCommand(
                Message: request.Message ?? string.Empty,
                Severity: request.Severity ?? "Info",
                IsActive: request.IsActive,
                StartsAt: request.StartsAt,
                EndsAt: request.EndsAt,
                UpdatedBy: updatedBy);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("UpdateStatusBanner")
        .WithTags("Admin", "Status Banner")
        .Produces<AdminStatusBannerResponse>(200)
        .ProducesValidationProblem()
        .ProducesProblem(401)
        .ProducesProblem(403);

        return app;
    }
}

/// <summary>
/// Issue #1089: HTTP request body for PUT /admin/status-banner.
/// </summary>
internal sealed record UpdateStatusBannerRequest(
    string? Message,
    string? Severity,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt);
