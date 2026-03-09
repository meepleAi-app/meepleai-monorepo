using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Issue #5476: Query to get all currently active emergency overrides.
/// </summary>
internal record GetActiveEmergencyOverridesQuery() : IQuery<IReadOnlyList<ActiveOverrideInfo>>;

internal class GetActiveEmergencyOverridesQueryHandler
    : IQueryHandler<GetActiveEmergencyOverridesQuery, IReadOnlyList<ActiveOverrideInfo>>
{
    private readonly IEmergencyOverrideService _overrideService;

    public GetActiveEmergencyOverridesQueryHandler(IEmergencyOverrideService overrideService)
    {
        _overrideService = overrideService;
    }

    public Task<IReadOnlyList<ActiveOverrideInfo>> Handle(
        GetActiveEmergencyOverridesQuery request,
        CancellationToken cancellationToken)
    {
        return _overrideService.GetActiveOverridesAsync(cancellationToken);
    }
}
