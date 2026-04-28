using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles public RSVP responses against a token-addressable invitation.
/// Implements the idempotency contract from spec §2.2 / D2 (b):
/// <list type="bullet">
///   <item>Same-status response → no-op (200 OK).</item>
///   <item>Switching Accepted ⇄ Declined → <see cref="ConflictException"/> (409).</item>
///   <item>Expired or Cancelled invitations → <see cref="GoneException"/> (410).</item>
/// </list>
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed class RespondToGameNightInvitationByTokenCommandHandler
    : ICommandHandler<RespondToGameNightInvitationByTokenCommand>
{
    private readonly IGameNightInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RespondToGameNightInvitationByTokenCommandHandler(
        IGameNightInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(
        RespondToGameNightInvitationByTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await _invitationRepository
            .GetByTokenAsync(command.Token, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightInvitation", command.Token);

        var utcNow = _timeProvider.GetUtcNow();

        // 410 Gone: terminal lifecycle states (cancelled by organizer or already expired).
        if (invitation.Status == GameNightInvitationStatus.Cancelled
            || invitation.Status == GameNightInvitationStatus.Expired)
        {
            throw new GoneException(
                $"This invitation is no longer available (status: {invitation.Status}).");
        }

        // 410 Gone: pending invitation past the cutoff.
        if (invitation.IsExpired(utcNow))
        {
            throw new GoneException("This invitation has expired.");
        }

        bool transitioned;
        try
        {
            transitioned = command.Response switch
            {
                GameNightInvitationStatus.Accepted => invitation.Accept(command.ResponderUserId, utcNow),
                GameNightInvitationStatus.Declined => invitation.Decline(command.ResponderUserId, utcNow),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(command),
                    $"Unsupported response status: {command.Response}"),
            };
        }
        catch (InvalidOperationException ex)
        {
            // After the expiry/terminal guards above, the only remaining failure
            // path is switching between Accepted ⇄ Declined → 409 Conflict.
            throw new ConflictException(ex.Message, ex);
        }

        // Idempotent same-state response: nothing changed, no need to persist.
        if (!transitioned)
        {
            return;
        }

        await _invitationRepository.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
