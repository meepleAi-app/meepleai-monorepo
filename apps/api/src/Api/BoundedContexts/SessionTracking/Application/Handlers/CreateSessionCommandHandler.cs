using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CreateSessionResult> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sessionType = Enum.Parse<SessionType>(request.SessionType);

        // Create session with retry for unique code
        Session session;
        const int maxRetries = 3;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            session = Session.Create(
                request.UserId,
                request.GameId,
                sessionType,
                request.Location,
                request.SessionDate);

            try
            {
                await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // Add additional participants (owner already added by factory)
                foreach (var participantDto in request.Participants.Where(p => !p.IsOwner))
                {
                    var participantInfo = ParticipantInfo.Create(
                        participantDto.DisplayName,
                        participantDto.IsOwner,
                        session.Participants.Count + 1);

                    session.AddParticipant(participantInfo, participantDto.UserId);
                }

                if (request.Participants.Count > 1)
                {
                    await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                return new CreateSessionResult(
                    session.Id,
                    session.SessionCode,
                    session.Participants.Select(MapParticipant).ToList());
            }
            catch (InvalidOperationException) when (attempt < maxRetries - 1)
            {
                // Session code collision, retry with new code
            }
        }

        throw new ConflictException("Unable to generate unique session code after retries");
    }

    private static ParticipantDto MapParticipant(Participant p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        DisplayName = p.DisplayName,
        IsOwner = p.IsOwner,
        JoinOrder = p.JoinOrder,
        FinalRank = p.FinalRank,
        TotalScore = 0
    };
}