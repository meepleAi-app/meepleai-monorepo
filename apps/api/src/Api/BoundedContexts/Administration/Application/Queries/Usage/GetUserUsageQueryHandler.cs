using Api.SharedKernel.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Usage;

/// <summary>
/// Handles <see cref="GetUserUsageQuery"/> by delegating to <see cref="ITierEnforcementService"/>.
/// E2-2: Game Night Improvvisata - User Usage Endpoint.
/// </summary>
internal sealed class GetUserUsageQueryHandler : IRequestHandler<GetUserUsageQuery, UsageSnapshot>
{
    private readonly ITierEnforcementService _tierService;

    public GetUserUsageQueryHandler(ITierEnforcementService tierService)
        => _tierService = tierService ?? throw new ArgumentNullException(nameof(tierService));

    public async Task<UsageSnapshot> Handle(GetUserUsageQuery request, CancellationToken cancellationToken)
        => await _tierService.GetUsageAsync(request.UserId, cancellationToken).ConfigureAwait(false);
}
