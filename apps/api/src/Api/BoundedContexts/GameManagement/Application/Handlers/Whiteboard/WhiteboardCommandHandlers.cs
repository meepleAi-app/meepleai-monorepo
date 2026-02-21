using Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.BoundedContexts.GameManagement.Application.Events;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Whiteboard;

/// <summary>
/// Initializes a new empty WhiteboardState for a session.
/// Validates the session exists and that no whiteboard has been initialized yet.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class InitializeWhiteboardCommandHandler
    : ICommandHandler<InitializeWhiteboardCommand, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InitializeWhiteboardCommandHandler(
        IWhiteboardStateRepository whiteboardRepository,
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WhiteboardStateDto> Handle(InitializeWhiteboardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        var existing = await _whiteboardRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
            throw new ConflictException("Whiteboard already initialized for this session.");

        var whiteboard = new WhiteboardState(Guid.NewGuid(), command.SessionId, command.UserId);

        await _whiteboardRepository.AddAsync(whiteboard, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WhiteboardMapper.ToDto(whiteboard);
    }
}

/// <summary>
/// Adds a freehand stroke to the whiteboard and broadcasts the SSE event.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class AddStrokeCommandHandler : ICommandHandler<AddStrokeCommand, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;
    private readonly ISessionBroadcastService _broadcastService;
    private readonly IUnitOfWork _unitOfWork;

    public AddStrokeCommandHandler(
        IWhiteboardStateRepository whiteboardRepository,
        ISessionBroadcastService broadcastService,
        IUnitOfWork unitOfWork)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WhiteboardStateDto> Handle(AddStrokeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var whiteboard = await _whiteboardRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("WhiteboardState", command.SessionId.ToString());

        try
        {
            whiteboard.AddStroke(command.StrokeId, command.DataJson, command.UserId);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _whiteboardRepository.UpdateAsync(whiteboard, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new StrokeAddedEvent(
            sessionId: command.SessionId,
            strokeId: command.StrokeId,
            dataJson: command.DataJson,
            modifiedBy: command.UserId);

        await _broadcastService.PublishAsync(
            command.SessionId,
            evt,
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return WhiteboardMapper.ToDto(whiteboard);
    }
}

/// <summary>
/// Removes a freehand stroke from the whiteboard and broadcasts the SSE event.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class RemoveStrokeCommandHandler : ICommandHandler<RemoveStrokeCommand, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;
    private readonly ISessionBroadcastService _broadcastService;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveStrokeCommandHandler(
        IWhiteboardStateRepository whiteboardRepository,
        ISessionBroadcastService broadcastService,
        IUnitOfWork unitOfWork)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WhiteboardStateDto> Handle(RemoveStrokeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var whiteboard = await _whiteboardRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("WhiteboardState", command.SessionId.ToString());

        try
        {
            whiteboard.RemoveStroke(command.StrokeId, command.UserId);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotFoundException("WhiteboardStroke", command.StrokeId, ex);
        }

        await _whiteboardRepository.UpdateAsync(whiteboard, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new StrokeRemovedEvent(
            sessionId: command.SessionId,
            strokeId: command.StrokeId,
            modifiedBy: command.UserId);

        await _broadcastService.PublishAsync(
            command.SessionId,
            evt,
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return WhiteboardMapper.ToDto(whiteboard);
    }
}

/// <summary>
/// Replaces the structured layer and broadcasts the SSE event.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class UpdateStructuredCommandHandler : ICommandHandler<UpdateStructuredCommand, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;
    private readonly ISessionBroadcastService _broadcastService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStructuredCommandHandler(
        IWhiteboardStateRepository whiteboardRepository,
        ISessionBroadcastService broadcastService,
        IUnitOfWork unitOfWork)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WhiteboardStateDto> Handle(UpdateStructuredCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var whiteboard = await _whiteboardRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("WhiteboardState", command.SessionId.ToString());

        whiteboard.UpdateStructured(command.StructuredJson, command.UserId);

        await _whiteboardRepository.UpdateAsync(whiteboard, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new StructuredUpdatedEvent(
            sessionId: command.SessionId,
            structuredJson: command.StructuredJson,
            modifiedBy: command.UserId);

        await _broadcastService.PublishAsync(
            command.SessionId,
            evt,
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return WhiteboardMapper.ToDto(whiteboard);
    }
}

/// <summary>
/// Clears all whiteboard strokes and resets the structured layer, broadcasting the SSE event.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal class ClearWhiteboardCommandHandler : ICommandHandler<ClearWhiteboardCommand, WhiteboardStateDto>
{
    private readonly IWhiteboardStateRepository _whiteboardRepository;
    private readonly ISessionBroadcastService _broadcastService;
    private readonly IUnitOfWork _unitOfWork;

    public ClearWhiteboardCommandHandler(
        IWhiteboardStateRepository whiteboardRepository,
        ISessionBroadcastService broadcastService,
        IUnitOfWork unitOfWork)
    {
        _whiteboardRepository = whiteboardRepository ?? throw new ArgumentNullException(nameof(whiteboardRepository));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WhiteboardStateDto> Handle(ClearWhiteboardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var whiteboard = await _whiteboardRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("WhiteboardState", command.SessionId.ToString());

        whiteboard.Clear(command.UserId);

        await _whiteboardRepository.UpdateAsync(whiteboard, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new WhiteboardClearedEvent(
            sessionId: command.SessionId,
            clearedBy: command.UserId);

        await _broadcastService.PublishAsync(
            command.SessionId,
            evt,
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return WhiteboardMapper.ToDto(whiteboard);
    }
}
