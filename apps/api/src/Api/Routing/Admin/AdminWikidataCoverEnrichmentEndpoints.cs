using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.Extensions;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1823 Wave 3 M12 — admin endpoint for the Wikidata cover enrichment
/// pipeline. Routes under <c>/api/v1/admin/wikidata/enrichment</c>. Admin-only
/// (gated by <see cref="RequireAdminSessionFilter"/>).
/// </summary>
/// <remarks>
/// <para>Endpoint summary:</para>
/// <list type="bullet">
///   <item><c>POST /{gameId:guid}</c> — manually trigger enrichment for one
///   shared game. Body: <c>{ "forceRefresh": bool }</c>. Returns the flattened
///   <see cref="AdminEnrichWikidataCoverResult"/> with outcome <c>"success"</c>
///   / <c>"skipped"</c> / <c>"failed"</c>.</item>
/// </list>
/// <para>CQRS compliance: dispatches exclusively via <see cref="IMediator"/>.</para>
/// </remarks>
internal static class AdminWikidataCoverEnrichmentEndpoints
{
    public static RouteGroupBuilder MapAdminWikidataCoverEnrichmentEndpoints(this RouteGroupBuilder group)
    {
        // Mirror of AdminCatalogSeedEndpoints: group-level admin auth filter.
        // Anonymous probes get 401; non-admin sessions get 403 BEFORE the
        // route handler runs.
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/{gameId:guid}", HandleTrigger)
            .WithName("AdminWikidataCoverEnrichment_Trigger")
            .WithTags("Admin", "WikidataCoverEnrichment");

        return group;
    }

    /// <summary>
    /// Request body for the trigger endpoint. <see cref="ForceRefresh"/> is
    /// optional — null is treated as <see langword="false"/>.
    /// </summary>
    public sealed record AdminTriggerWikidataEnrichmentRequest(bool? ForceRefresh);

    private static async Task<IResult> HandleTrigger(
        Guid gameId,
        AdminTriggerWikidataEnrichmentRequest? request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Auth already enforced at the group level via RequireAdminSessionFilter.
        // GameId is parsed by the route constraint :guid — an invalid GUID
        // returns 400 BEFORE this handler runs. The request body is optional;
        // null body is treated as forceRefresh=false.
        var command = new AdminEnrichWikidataCoverCommand(
            GameId: gameId,
            ForceRefresh: request?.ForceRefresh ?? false,
            TriggeredByUserId: context.User.GetUserId());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
