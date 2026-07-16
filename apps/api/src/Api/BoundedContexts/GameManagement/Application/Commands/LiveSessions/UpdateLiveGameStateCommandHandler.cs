using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles <see cref="UpdateLiveGameStateCommand"/>: replaces the live session's opaque
/// game-state (host/participant only) and streams the change (#3025 L1).
///
/// Pattern mirrors <see cref="AddDiaryEntryCommandHandler"/>. ADR-060: <c>SaveChangesAsync</c>
/// after <c>UpdateAsync</c>; the <see cref="Domain.Events.LiveSessionGameStateEvent"/> raised
/// by the domain dispatches post-commit. <see cref="ConflictException"/> (Completed session)
/// propagates → HTTP 409.
/// </summary>
internal sealed class UpdateLiveGameStateCommandHandler : ICommandHandler<UpdateLiveGameStateCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLiveGameStateCommandHandler(
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(UpdateLiveGameStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository
            .GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Authz: caller must be the session creator or an active linked player.
        if (!session.IsAuthorizedParticipant(command.RequestedByUserId))
            throw new ForbiddenException("Only the session creator or an active participant may update game state.");

        // Parse the JsonElement into an OWNED JsonDocument. UpdateGameState takes ownership
        // (stores it + disposes the prior). Do NOT wrap in `using` — the aggregate owns it now.
        var doc = System.Text.Json.JsonDocument.Parse(command.State.GetRawText());
        session.UpdateGameState(doc);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
