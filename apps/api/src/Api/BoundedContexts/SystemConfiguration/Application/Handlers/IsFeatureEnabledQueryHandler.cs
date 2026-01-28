using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for checking if a feature is enabled for a specific role.
/// Hierarchy: Role-specific flag > Global flag > Default false.
/// </summary>
internal class IsFeatureEnabledQueryHandler : IQueryHandler<IsFeatureEnabledQuery, bool>
{
    private readonly IFeatureFlagService _featureFlagService;

    public IsFeatureEnabledQueryHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<bool> Handle(IsFeatureEnabledQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _featureFlagService.IsEnabledAsync(query.FeatureName, query.Role).ConfigureAwait(false);
    }
}
