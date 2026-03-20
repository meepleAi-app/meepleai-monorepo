using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

// ============================================================================
// Query handler
// ============================================================================

/// <summary>
/// Returns the current widget states for a session's toolkit.
/// Returns null if no toolkit state has been saved yet.
/// Issue #5148 — Epic B5.
/// </summary>
internal sealed class GetToolkitSessionStateQueryHandler
    : IRequestHandler<GetToolkitSessionStateQuery, ToolkitSessionStateDto?>
{
    private readonly IToolkitSessionStateRepository _repository;
    private readonly ISessionRepository _sessionRepository;

    public GetToolkitSessionStateQueryHandler(
        IToolkitSessionStateRepository repository,
        ISessionRepository sessionRepository)
    {
        _repository = repository;
        _sessionRepository = sessionRepository;
    }

    public async Task<ToolkitSessionStateDto?> Handle(
        GetToolkitSessionStateQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameSession", request.SessionId.ToString());

        if (session.UserId != request.UserId &&
            !session.Participants.Any(p => p.UserId == request.UserId))
        {
            throw new ForbiddenException("You do not have access to this session.");
        }

        var state = await _repository
            .GetBySessionAsync(request.SessionId, request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        return state is null ? null : ToolkitSessionStateMapper.ToDto(state);
    }
}

// ============================================================================
// Command handler
// ============================================================================

/// <summary>
/// Updates a widget's runtime state. Creates the ToolkitSessionState if it doesn't exist.
/// Issue #5148 — Epic B5.
/// </summary>
internal sealed class UpdateWidgetStateCommandHandler
    : IRequestHandler<UpdateWidgetStateCommand, ToolkitSessionStateDto>
{
    private readonly IToolkitSessionStateRepository _repository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public UpdateWidgetStateCommandHandler(
        IToolkitSessionStateRepository repository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService)
    {
        _repository = repository;
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
        _syncService = syncService;
    }

    public async Task<ToolkitSessionStateDto> Handle(
        UpdateWidgetStateCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameSession", request.SessionId.ToString());

        if (session.UserId != request.UserId &&
            !session.Participants.Any(p => p.UserId == request.UserId))
        {
            throw new ForbiddenException("You do not have access to this session.");
        }

        var state = await _repository
            .GetBySessionAsync(request.SessionId, request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            state = ToolkitSessionState.Create(request.SessionId, request.ToolkitId);
            await _repository.AddAsync(state, cancellationToken).ConfigureAwait(false);
        }

        state.UpdateWidgetState(request.WidgetType, request.StateJson);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Broadcast widget state change to all session participants via SSE
        var evt = new WidgetStateUpdatedEvent(
            request.SessionId,
            request.ToolkitId,
            request.WidgetType,
            request.StateJson,
            request.UserId,
            DateTime.UtcNow);
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken)
            .ConfigureAwait(false);

        return ToolkitSessionStateMapper.ToDto(state);
    }
}

// ============================================================================
// Shared mapper
// ============================================================================

file static class ToolkitSessionStateMapper
{
    internal static ToolkitSessionStateDto ToDto(ToolkitSessionState s) =>
        new(s.Id, s.SessionId, s.ToolkitId, s.WidgetStates, s.CreatedAt, s.UpdatedAt);
}
