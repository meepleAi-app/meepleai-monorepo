using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles adding a new event to the session timeline.
/// Validates session exists and is active before persisting.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public class AddSessionEventCommandHandler : IRequestHandler<AddSessionEventCommand, AddSessionEventResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _sessionEventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddSessionEventCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _sessionEventRepository = sessionEventRepository ?? throw new ArgumentNullException(nameof(sessionEventRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AddSessionEventResult> Handle(AddSessionEventCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot add events to session with status {session.Status}");

        var sessionEvent = SessionEvent.Create(
            request.SessionId,
            request.EventType,
            request.Payload,
            request.RequesterId,
            request.Source);

        await _sessionEventRepository.AddAsync(sessionEvent, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddSessionEventResult(
            sessionEvent.Id,
            sessionEvent.EventType,
            sessionEvent.Timestamp);
    }
}
