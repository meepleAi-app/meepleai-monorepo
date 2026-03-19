using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetDashboardStreamQuery - Provides SSE event stream for dashboard.
/// Any authenticated user can subscribe to their dashboard stream.
/// </summary>
public class GetDashboardStreamQueryHandler : IRequestHandler<GetDashboardStreamQuery, IAsyncEnumerable<INotification>>
{
    private readonly IDashboardStreamService _streamService;

    public GetDashboardStreamQueryHandler(IDashboardStreamService streamService)
    {
        _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
    }

    public Task<IAsyncEnumerable<INotification>> Handle(GetDashboardStreamQuery request, CancellationToken cancellationToken)
    {
        // Return subscription stream - authentication is handled at endpoint level
        return Task.FromResult(_streamService.SubscribeToDashboardEvents(request.UserId, cancellationToken));
    }
}
