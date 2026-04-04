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

public class CreateSessionCheckpointCommandHandler
    : IRequestHandler<CreateSessionCheckpointCommand, CreateSessionCheckpointResult>
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionCheckpointRepository _checkpointRepository;
    private readonly ISessionEventRepository _sessionEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;

    public CreateSessionCheckpointCommandHandler(
        ISessionRepository sessionRepository,
        ISessionCheckpointRepository checkpointRepository,
        ISessionEventRepository sessionEventRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _checkpointRepository = checkpointRepository ?? throw new ArgumentNullException(nameof(checkpointRepository));
        _sessionEventRepository = sessionEventRepository ?? throw new ArgumentNullException(nameof(sessionEventRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<CreateSessionCheckpointResult> Handle(
        CreateSessionCheckpointCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot create checkpoint for session with status {session.Status}");

        var toolkitStates = await _db.ToolkitSessionStates
            .AsNoTracking()
            .Where(t => t.SessionId == request.SessionId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var snapshots = toolkitStates.Select(t => new
        {
            t.ToolkitId,
            WidgetStatesJson = t.GetWidgetStatesJson(),
            t.UpdatedAt
        });

        var snapshotData = JsonSerializer.Serialize(snapshots, SnapshotJsonOptions);

        var diaryEventCount = await _sessionEventRepository
            .CountBySessionIdAsync(request.SessionId, ct: cancellationToken).ConfigureAwait(false);

        var checkpoint = SessionCheckpoint.Create(
            request.SessionId, request.Name, request.RequesterId,
            snapshotData, diaryEventCount);

        await _checkpointRepository.AddAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateSessionCheckpointResult(
            checkpoint.Id, checkpoint.Name, checkpoint.CreatedAt, checkpoint.DiaryEventCount);
    }
}
