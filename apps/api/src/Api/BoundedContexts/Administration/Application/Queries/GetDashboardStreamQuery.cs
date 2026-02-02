using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get SSE event stream for dashboard with authorization.
/// Returns async enumerable for Server-Sent Events streaming.
/// </summary>
public record GetDashboardStreamQuery(
    Guid UserId
) : IRequest<IAsyncEnumerable<INotification>>;
