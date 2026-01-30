using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class AddParticipantCommandHandler : IRequestHandler<AddParticipantCommand, AddParticipantResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddParticipantCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AddParticipantResult> Handle(AddParticipantCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists and is not finalized
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status == SessionStatus.Finalized)
        {
            throw new ConflictException("Cannot add participant to finalized session");
        }

        // Verify max participants not exceeded
        if (session.Participants.Count >= 20)
        {
            throw new ConflictException("Maximum 20 participants allowed per session");
        }

        // Calculate join order
        var joinOrder = session.Participants.Count > 0
            ? session.Participants.Max(p => p.JoinOrder) + 1
            : 1;

        // Create participant info and add to session
        var participantInfo = ParticipantInfo.Create(
            request.DisplayName,
            false, // isOwner
            joinOrder
        );

        session.AddParticipant(participantInfo, request.UserId);

        // Save via repository
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Get the newly created participant ID
        var newParticipant = session.Participants.First(p => p.JoinOrder == joinOrder);


        return new AddParticipantResult(
            newParticipant.Id,
            joinOrder
        );
    }
}