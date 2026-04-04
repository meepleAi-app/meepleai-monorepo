using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class ListSessionCheckpointsQueryHandler
    : IRequestHandler<ListSessionCheckpointsQuery, ListSessionCheckpointsResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionCheckpointRepository _checkpointRepository;

    public ListSessionCheckpointsQueryHandler(
        ISessionRepository sessionRepository,
        ISessionCheckpointRepository checkpointRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _checkpointRepository = checkpointRepository ?? throw new ArgumentNullException(nameof(checkpointRepository));
    }

    public async Task<ListSessionCheckpointsResult> Handle(
        ListSessionCheckpointsQuery request, CancellationToken cancellationToken)
    {
        _ = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        var checkpoints = await _checkpointRepository
            .GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var dtos = checkpoints.Select(c => new SessionCheckpointDto(
            c.Id, c.Name, c.CreatedAt, c.CreatedBy, c.DiaryEventCount)).ToList();

        return new ListSessionCheckpointsResult(dtos);
    }
}
