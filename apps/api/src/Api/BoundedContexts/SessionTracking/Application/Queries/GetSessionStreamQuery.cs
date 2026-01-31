using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get SSE event stream for a session with authorization.
/// Returns async enumerable for Server-Sent Events streaming.
/// </summary>
public record GetSessionStreamQuery(
    Guid SessionId,
    Guid UserId
) : IRequest<IAsyncEnumerable<INotification>>;
