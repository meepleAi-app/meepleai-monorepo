using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetActiveAlertsQuery.
/// Delegates to AlertingService (service will be refactored in future iterations).
/// </summary>
public class GetActiveAlertsQueryHandler : IQueryHandler<GetActiveAlertsQuery, List<AlertDto>>
{
    private readonly IAlertingService _alertingService;

    public GetActiveAlertsQueryHandler(IAlertingService alertingService)
    {
        _alertingService = alertingService;
    }

    public async Task<List<AlertDto>> Handle(GetActiveAlertsQuery query, CancellationToken cancellationToken)
    {
        return await _alertingService.GetActiveAlertsAsync(cancellationToken);
    }
}
