using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetAlertHistoryQuery.
/// Delegates to AlertingService (service will be refactored in future iterations).
/// </summary>
internal class GetAlertHistoryQueryHandler : IQueryHandler<GetAlertHistoryQuery, List<AlertDto>>
{
    private readonly IAlertingService _alertingService;

    public GetAlertHistoryQueryHandler(IAlertingService alertingService)
    {
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
    }

    public async Task<List<AlertDto>> Handle(GetAlertHistoryQuery query, CancellationToken cancellationToken)
    {
        return await _alertingService.GetAlertHistoryAsync(
            query.FromDate,
            query.ToDate,
            cancellationToken
        ).ConfigureAwait(false);
    }
}
