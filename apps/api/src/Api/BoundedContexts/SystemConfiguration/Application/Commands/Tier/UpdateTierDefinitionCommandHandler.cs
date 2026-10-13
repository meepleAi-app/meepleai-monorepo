using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

/// <summary>
/// Handler that updates an existing tier definition's limits, LLM model tier, or default status.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal class UpdateTierDefinitionCommandHandler
    : IRequestHandler<UpdateTierDefinitionCommand, TierDefinitionDto>
{
    private readonly MeepleAiDbContext _db;

    private readonly IMemoryCache _cache;

    public UpdateTierDefinitionCommandHandler(MeepleAiDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<TierDefinitionDto> Handle(
        UpdateTierDefinitionCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.ToLowerInvariant();
        // #4212: AsTracking — the context defaults to NoTracking (#3866), and an untracked plan
        // made SaveChanges a no-op: every admin edit answered with the new values and was lost.
        var tier = await _db.TierDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Name == normalizedName, cancellationToken)
            .ConfigureAwait(false);

        if (tier is null)
            throw new KeyNotFoundException($"Tier '{request.Name}' not found");

        if (request.Limits is not null)
            tier.UpdateLimits(request.Limits.ToValueObject());

        if (request.LlmModelTier is not null)
            tier.UpdateLlmModelTier(request.LlmModelTier);

        if (request.IsDefault.HasValue)
            tier.SetDefault(request.IsDefault.Value);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4212: TierEnforcementService caches a plan for five minutes. Without the eviction an
        // admin edit would reach the limits it governs up to five minutes later.
        _cache.Remove(TierEnforcementService.TierDefinitionCacheKey(tier.Name));

        return TierDefinitionDto.FromEntity(tier);
    }
}
