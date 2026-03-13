using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;

/// <summary>
/// Handler that retrieves all tier definitions ordered by name.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal class GetAllTierDefinitionsQueryHandler
    : IRequestHandler<GetAllTierDefinitionsQuery, IReadOnlyList<TierDefinitionDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetAllTierDefinitionsQueryHandler(MeepleAiDbContext db) => _db = db;

    public async Task<IReadOnlyList<TierDefinitionDto>> Handle(
        GetAllTierDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var tiers = await _db.TierDefinitions
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tiers.Select(TierDefinitionDto.FromEntity).ToList();
    }
}
