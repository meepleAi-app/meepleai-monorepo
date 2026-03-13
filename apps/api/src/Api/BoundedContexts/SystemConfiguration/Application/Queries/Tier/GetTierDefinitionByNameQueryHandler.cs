using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;

/// <summary>
/// Handler that retrieves a tier definition by its normalized name.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal class GetTierDefinitionByNameQueryHandler
    : IRequestHandler<GetTierDefinitionByNameQuery, TierDefinitionDto?>
{
    private readonly MeepleAiDbContext _db;

    public GetTierDefinitionByNameQueryHandler(MeepleAiDbContext db) => _db = db;

    public async Task<TierDefinitionDto?> Handle(
        GetTierDefinitionByNameQuery request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.ToLowerInvariant();
        var tier = await _db.TierDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == normalizedName, cancellationToken)
            .ConfigureAwait(false);

        return tier is null ? null : TierDefinitionDto.FromEntity(tier);
    }
}
