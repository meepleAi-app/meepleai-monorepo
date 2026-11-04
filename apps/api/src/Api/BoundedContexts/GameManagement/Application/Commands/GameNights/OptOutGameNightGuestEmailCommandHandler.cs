using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Puts the address behind an invitation on the opt-out list (#4846). Idempotent: an address already on it, and
/// a second tap that loses the race for the unique index, both end as a success, because the guest got what they
/// asked for.
/// </summary>
/// <remarks>
/// The invitation is read whatever its state. A guest can hold a link to a night that was answered, cancelled
/// or has expired, and that is precisely when they want to stop hearing about it; the RSVP rules (410 Gone for a
/// terminal invitation) do not apply to «stop writing to me».
/// </remarks>
internal sealed class OptOutGameNightGuestEmailCommandHandler : ICommandHandler<OptOutGameNightGuestEmailCommand>
{
    /// <summary>Postgres unique_violation SQLSTATE.</summary>
    private const string UniqueViolationSqlState = "23505";

    private readonly IGameNightInvitationRepository _invitationRepository;
    private readonly IGameNightGuestEmailOptOutRepository _optOutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public OptOutGameNightGuestEmailCommandHandler(
        IGameNightInvitationRepository invitationRepository,
        IGameNightGuestEmailOptOutRepository optOutRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _optOutRepository = optOutRepository ?? throw new ArgumentNullException(nameof(optOutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(OptOutGameNightGuestEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await _invitationRepository
            .GetByTokenAsync(command.Token, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightInvitation", command.Token);

        if (await _optOutRepository.ExistsAsync(invitation.Email, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _optOutRepository
            .AddAsync(GameNightGuestEmailOptOut.Create(invitation.Email, invitation.Id, _timeProvider.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // Another request put the same address on the list between our check and our insert: the
            // unique index kept it to one entry, and the outcome is the one the guest asked for.
        }
    }
}
