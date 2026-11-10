using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles cancellation of a game night event.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Uses NotFoundException, calls UpdateAsync, publishes domain events via aggregate.
/// </summary>
/// <remarks>
/// <para>
/// #4856: the cancellation changes the NIGHT, not its invitations (another aggregate), and the public page behind
/// each invitation link is cached for a minute under a per-token tag. The tag of every invitation of the night is
/// dropped here, in the handler and not from the cancellation event: the events of staging and production run from
/// the outbox seconds later, after the emails to the guests, and the link would keep showing the night meanwhile
/// (<c>CancelGameNightInvitationCommandHandler</c> drops its own page the same way, #4698). It is done even when
/// the night was already cancelled, so a retry repairs a drop that failed the first time.
/// </para>
/// <para>
/// Best effort: the cancellation is committed, so a failure to drop (an L2 that is down) is a warning and not a 500
/// that makes the organizer repeat a done thing; the page expires within a minute anyway. It evicts the
/// distributed entry and this process's local one only.
/// </para>
/// </remarks>
internal sealed class CancelGameNightCommandHandler : ICommandHandler<CancelGameNightCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IGameNightInvitationRepository _invitations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;
    private readonly ILogger<CancelGameNightCommandHandler> _logger;

    public CancelGameNightCommandHandler(
        IGameNightEventRepository repository,
        IGameNightInvitationRepository invitations,
        IUnitOfWork unitOfWork,
        HybridCache cache,
        ILogger<CancelGameNightCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CancelGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new UnauthorizedAccessException("Only the organizer can cancel a game night");

        try
        {
            gameNight.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await DropCachedPagesAsync(gameNight.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task DropCachedPagesAsync(Guid gameNightId, CancellationToken cancellationToken)
    {
        IReadOnlyList<GameNightInvitation> invitations;
        try
        {
            invitations = await _invitations.GetByGameNightIdAsync(gameNightId, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Best effort after a committed cancellation: see the remarks of the handler
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not list the invitations of cancelled game night {GameNightId} to drop their cached pages; they expire on their own",
                gameNightId);
            return;
        }
#pragma warning restore CA1031

        foreach (var invitation in invitations)
        {
            try
            {
                await _cache
                    .RemoveByTagAsync(GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + invitation.Token, cancellationToken)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Best effort after a committed cancellation: see the remarks of the handler
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Could not drop the cached page of an invitation of cancelled game night {GameNightId}; it expires on its own",
                    gameNightId);
            }
#pragma warning restore CA1031
        }
    }
}
