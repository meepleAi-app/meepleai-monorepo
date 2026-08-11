using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles <see cref="AddDiaryEntryCommand"/>: appends an immutable diary entry to a live game session
/// and returns the new entry's id.
///
/// Pattern mirrors <c>UpdateSetupChecklistCommandHandler</c> / <c>UpdateLiveSessionNotesCommandHandler</c>.
/// ADR-060: every mutating handler calls <c>_unitOfWork.SaveChangesAsync</c> after <c>UpdateAsync</c>.
/// The <see cref="Api.Middleware.Exceptions.ConflictException"/> raised by the domain for a Completed session
/// propagates unhandled — the middleware maps it to HTTP 409.
///
/// Issue #2570 SP3 T3.
/// </summary>
internal sealed class AddDiaryEntryCommandHandler : ICommandHandler<AddDiaryEntryCommand, Guid>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddDiaryEntryCommandHandler(
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Guid> Handle(AddDiaryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository
            .GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Authz: caller must be the session creator or an active linked player.
        // Mirrors GetLiveSessionStreamContextQueryHandler (SP2 T4).
        var isParticipant = session.IsAuthorizedParticipant(command.AuthorId);
        if (!isParticipant)
            throw new ForbiddenException("Only the session creator or an active participant may add diary entries.");

        // ConflictException from a Completed session propagates to the middleware (HTTP 409).
        session.AddDiaryEntry(command.AuthorId, command.Text);

        await _sessionRepository
            .UpdateAsync(session, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The new entry is always appended last (append-only invariant, T1).
        return session.DiaryEntries[^1].Id;
    }
}
