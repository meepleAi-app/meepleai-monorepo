using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Filters;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;

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
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var result = new List<object>();
            foreach (var enhancement in Enum.GetValues<RagEnhancement>())
            {
                if (enhancement == RagEnhancement.None) continue;
                var flagKey = enhancement.ToFeatureFlagKey();
                var isEnabled = await featureFlags.IsEnabledAsync(flagKey).ConfigureAwait(false);
                result.Add(new
                {
                    Name = enhancement.ToString(),
                    FlagKey = flagKey,
                    IsGloballyEnabled = isEnabled,
                    ExtraCreditsBalanced = enhancement.GetExtraCredits(useBalancedForAux: true),
                    ExtraCreditsFast = enhancement.GetExtraCredits(useBalancedForAux: false)
                });
            }
            return Results.Ok(result);
        })
        .WithName("ListRagEnhancements")
        .WithSummary("List all RAG enhancements with their current status");

        // POST /api/v1/admin/rag-enhancements/{key}/toggle — Toggle enhancement globally
        ragGroup.MapPost("/{key}/toggle", async (
            string key,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var validKeys = FeatureFlagConstants.RagEnhancements;
            if (!validKeys.Contains(key, StringComparer.Ordinal))
                return Results.BadRequest(new { Error = $"Unknown enhancement key: {key}" });

            var isEnabled = await featureFlags.IsEnabledAsync(key).ConfigureAwait(false);
            if (isEnabled)
                await featureFlags.DisableFeatureAsync(key).ConfigureAwait(false);
            else
                await featureFlags.EnableFeatureAsync(key).ConfigureAwait(false);

            return Results.Ok(new { Key = key, IsNowEnabled = !isEnabled });
        })
        .WithName("ToggleRagEnhancement")
        .WithSummary("Toggle a RAG enhancement on/off globally");

        // POST /api/v1/admin/rag-enhancements/{key}/tier/{tierName}/toggle — Toggle per tier
        ragGroup.MapPost("/{key}/tier/{tierName}/toggle", async (
            string key,
            string tierName,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var validKeys = FeatureFlagConstants.RagEnhancements;
            if (!validKeys.Contains(key, StringComparer.Ordinal))
                return Results.BadRequest(new { Error = $"Unknown enhancement key: {key}" });

            var tier = UserTier.Parse(tierName);
            var isEnabled = await featureFlags.IsEnabledForTierAsync(key, tier).ConfigureAwait(false);
            if (isEnabled)
                await featureFlags.DisableFeatureForTierAsync(key, tier).ConfigureAwait(false);
            else
                await featureFlags.EnableFeatureForTierAsync(key, tier).ConfigureAwait(false);

            return Results.Ok(new { Key = key, Tier = tierName, IsNowEnabled = !isEnabled });
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
            IRagEnhancementService enhancementService,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var tierClaim = ctx.User?.FindFirst("tier")?.Value ?? "Free";
            var tier = UserTier.Parse(tierClaim);

            var active = await enhancementService.GetActiveEnhancementsAsync(tier, ct).ConfigureAwait(false);
            var extraCredits = await enhancementService.EstimateExtraCreditsAsync(active, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                ActiveEnhancements = active.ToString(),
                ActiveFlags = active.GetIndividualFlags().Select(f => f.ToString()),
                ExtraCreditsPerQuery = extraCredits,
                UseBalancedAuxModel = await enhancementService.UseBalancedAuxModelAsync(ct).ConfigureAwait(false)
            });
        })
        .WithName("EstimateRagEnhancementCost")
        .WithSummary("Get active RAG enhancements and estimated extra credits for current user");

        return group;
    }
}
