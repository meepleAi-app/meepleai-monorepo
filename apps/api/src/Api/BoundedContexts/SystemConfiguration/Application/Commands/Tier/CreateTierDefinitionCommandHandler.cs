using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

/// <summary>
/// Handler that creates a new tier definition with duplicate-name validation.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal class CreateTierDefinitionCommandHandler
    : IRequestHandler<CreateTierDefinitionCommand, TierDefinitionDto>
{
    private readonly MeepleAiDbContext _db;

    public CreateTierDefinitionCommandHandler(MeepleAiDbContext db) => _db = db;

    public async Task<TierDefinitionDto> Handle(
        CreateTierDefinitionCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.ToLowerInvariant();
        var existing = await _db.TierDefinitions
            .AnyAsync(t => t.Name == normalizedName, cancellationToken)
            .ConfigureAwait(false);

        if (existing)
            throw new InvalidOperationException($"Tier '{request.Name}' already exists");

        var limits = request.Limits.ToValueObject();
        var tier = TierDefinition.Create(
            request.Name, request.DisplayName, limits, request.LlmModelTier, request.IsDefault);

        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TierDefinitionDto.FromEntity(tier);
    }
}
