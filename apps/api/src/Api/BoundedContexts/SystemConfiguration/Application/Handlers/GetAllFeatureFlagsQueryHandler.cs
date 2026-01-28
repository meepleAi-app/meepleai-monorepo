using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for retrieving all feature flags with their current states.
/// Issue #3073: Includes tier restrictions for tier-based feature flags.
/// </summary>
internal class GetAllFeatureFlagsQueryHandler : IQueryHandler<GetAllFeatureFlagsQuery, List<FeatureFlagDto>>
{
    private readonly IFeatureFlagService _featureFlagService;

    public GetAllFeatureFlagsQueryHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<List<FeatureFlagDto>> Handle(GetAllFeatureFlagsQuery query, CancellationToken cancellationToken)
    {
        return await _featureFlagService.GetAllFeatureFlagsAsync().ConfigureAwait(false);
    }
}
