using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagEnhancements;
using Api.Extensions;
using Api.Filters;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for RAG enhancement toggles and user-facing cost estimates.
/// Provides global and per-tier feature flag management for RAG pipeline enhancements.
/// </summary>
internal static class RagEnhancementAdminEndpoints
{
    public static RouteGroupBuilder MapRagEnhancementAdminEndpoints(this RouteGroupBuilder group)
    {
        var ragGroup = group.MapGroup("/admin/rag-enhancements")
            .WithTags("Admin", "RAG Enhancements")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/rag-enhancements — List all enhancements with status
        ragGroup.MapGet("/", async (
            IMediator mediator,
            CancellationToken ct) =>
            Results.Ok(await mediator.Send(new ListRagEnhancementsQuery(), ct).ConfigureAwait(false)))
        .WithName("ListRagEnhancements")
        .WithSummary("List all RAG enhancements with their current status");

        // POST /api/v1/admin/rag-enhancements/{key}/toggle — Toggle enhancement globally
        ragGroup.MapPost("/{key}/toggle", async (
            string key,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!IsRagEnhancementKey(key))
                return Results.BadRequest(new { Error = $"Unknown enhancement key: {key}" });

            var isNowEnabled = await mediator
                .Send(new ToggleRagEnhancementCommand(key, AdminId(context)), ct)
                .ConfigureAwait(false);

            return Results.Ok(new { Key = key, IsNowEnabled = isNowEnabled });
        })
        .WithName("ToggleRagEnhancement")
        .WithSummary("Toggle a RAG enhancement on/off globally");

        // POST /api/v1/admin/rag-enhancements/{key}/tier/{tierName}/toggle — Toggle per tier
        ragGroup.MapPost("/{key}/tier/{tierName}/toggle", async (
            string key,
            string tierName,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!IsRagEnhancementKey(key))
                return Results.BadRequest(new { Error = $"Unknown enhancement key: {key}" });

            // An unknown tier throws the domain ValidationException, which the middleware answers with 400.
            var tier = UserTier.Parse(tierName);
            var isNowEnabled = await mediator
                .Send(new ToggleRagEnhancementCommand(key, AdminId(context), tier), ct)
                .ConfigureAwait(false);

            return Results.Ok(new { Key = key, Tier = tierName, IsNowEnabled = isNowEnabled });
        })
        .WithName("ToggleRagEnhancementForTier")
        .WithSummary("Toggle a RAG enhancement on/off for a specific tier");

        return group;
    }

    /// <summary>
    /// User-facing endpoint for RAG enhancement cost estimates (no admin filter).
    /// </summary>
    public static RouteGroupBuilder MapRagEnhancementEstimateEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/rag/enhancements/estimate — User-facing cost estimate
        group.MapGet("/rag/enhancements/estimate", async (
            IMediator mediator,
            HttpContext ctx,
            CancellationToken ct) =>
            Results.Ok(await mediator.Send(new EstimateRagEnhancementCostQuery(TierOf(ctx)), ct).ConfigureAwait(false)))
        .WithName("EstimateRagEnhancementCost")
        .WithSummary("Get active RAG enhancements and estimated extra credits for current user");

        return group;
    }

    private static bool IsRagEnhancementKey(string key) =>
        FeatureFlagConstants.RagEnhancements.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// The admin who sent the request, saved as the author of the flag row. <c>RequireAdminSessionFilter</c> on
    /// the group has already required a valid session, so the cast cannot fail here. EffectiveActor
    /// (Principal.cs) is the real admin also during an impersonation. #4594: the routes passed no id, the
    /// author was <see cref="Guid.Empty"/> and the request was rejected with 422.
    /// </summary>
    private static Guid AdminId(HttpContext context) =>
        ((SessionStatusDto)context.Items[nameof(SessionStatusDto)]!).Principal!.EffectiveActor.Id;

    /// <summary>
    /// The tier the estimate is computed for: the tier of the user of the session, or Free without one
    /// (the route is reachable anonymously). #4580: this read a "tier" claim that the session handler does
    /// not emit, so everybody was estimated as Free. The tier travels in the session (<c>UserDto.Tier</c>).
    /// Subject, not EffectiveActor: the tier is the one of the user whose quota is in scope, also during
    /// an impersonation (Principal.cs).
    /// </summary>
    private static UserTier TierOf(HttpContext ctx)
    {
        var (authenticated, session, _) = ctx.TryGetActiveSession();
        var tier = authenticated ? session.Principal!.Subject.Tier : null;
        return UserTier.IsValid(tier) ? UserTier.Parse(tier!) : UserTier.Free;
    }
}
