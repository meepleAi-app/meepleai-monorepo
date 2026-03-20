using System.Text.Json;
using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class RestoreSessionCheckpointCommandHandler
    : IRequestHandler<RestoreSessionCheckpointCommand, RestoreSessionCheckpointResult>
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionCheckpointRepository _checkpointRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;

    public RestoreSessionCheckpointCommandHandler(
        ISessionRepository sessionRepository,
        ISessionCheckpointRepository checkpointRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _checkpointRepository = checkpointRepository ?? throw new ArgumentNullException(nameof(checkpointRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<RestoreSessionCheckpointResult> Handle(
        RestoreSessionCheckpointCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot restore checkpoint for session with status {session.Status}");

        var checkpoint = await _checkpointRepository
            .GetByIdAsync(request.CheckpointId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Checkpoint {request.CheckpointId} not found");

        if (checkpoint.SessionId != request.SessionId)
            throw new ConflictException("Checkpoint does not belong to the specified session");

        var snapshots = JsonSerializer.Deserialize<List<CheckpointSnapshot>>(
            checkpoint.SnapshotData, SnapshotJsonOptions) ?? [];

        var widgetsRestored = 0;
        foreach (var snapshot in snapshots)
        {
            var existingState = await _db.ToolkitSessionStates
                .FirstOrDefaultAsync(
                    t => t.SessionId == request.SessionId && t.ToolkitId == snapshot.ToolkitId,
                    cancellationToken).ConfigureAwait(false);

            if (existingState != null)
            {
                existingState.RestoreWidgetStates(snapshot.WidgetStatesJson);
                widgetsRestored++;
            }
            else
            {
                var newState = ToolkitSessionState.Create(request.SessionId, snapshot.ToolkitId);
                newState.RestoreWidgetStates(snapshot.WidgetStatesJson);
                _db.ToolkitSessionStates.Add(newState);
                widgetsRestored++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RestoreSessionCheckpointResult(
            checkpoint.Id, checkpoint.Name, DateTime.UtcNow, widgetsRestored);
    }

    private sealed record CheckpointSnapshot(
        Guid ToolkitId, string WidgetStatesJson, DateTime UpdatedAt);
}
