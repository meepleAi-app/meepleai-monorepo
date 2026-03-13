using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

/// <summary>
/// Handler that updates an existing tier definition's limits, LLM model tier, or default status.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal class UpdateTierDefinitionCommandHandler
    : IRequestHandler<UpdateTierDefinitionCommand, TierDefinitionDto>
{
    private readonly MeepleAiDbContext _db;

    public UpdateTierDefinitionCommandHandler(MeepleAiDbContext db) => _db = db;

    public async Task<TierDefinitionDto> Handle(
        UpdateTierDefinitionCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.ToLowerInvariant();
        var tier = await _db.TierDefinitions
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

        return TierDefinitionDto.FromEntity(tier);
    }
}
