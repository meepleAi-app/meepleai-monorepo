using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles the organizer's withdrawal of an emailed invitation (#4721).
/// </summary>
/// <remarks>
/// <para>
/// The public invitation page is cached for a minute under a per-token tag. The tag is dropped here,
/// in the handler, and not from a domain event: the events of staging and production run from the outbox
/// seconds later, and the organizer would see the link still working (#4698 was the same mistake).
/// Injects the raw <see cref="HybridCache"/>, like <c>GameNightInvitationRespondedHandler</c>, to share the
/// native tag index with the query.
/// </para>
/// <para>
/// Dropping the tag is best effort: the withdrawal is already committed, so a failure there (an L2 that is
/// down) must not turn a done thing into a 500 that makes the organizer repeat it. The cached page expires
/// within a minute anyway.
/// </para>
/// </remarks>
internal sealed class CancelGameNightInvitationCommandHandler : ICommandHandler<CancelGameNightInvitationCommand>
{
    private readonly IGameNightInvitationRepository _invitations;
    private readonly IGameNightEventRepository _gameNights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CancelGameNightInvitationCommandHandler> _logger;

    public CancelGameNightInvitationCommandHandler(
        IGameNightInvitationRepository invitations,
        IGameNightEventRepository gameNights,
        IUnitOfWork unitOfWork,
        HybridCache cache,
        TimeProvider timeProvider,
        ILogger<CancelGameNightInvitationCommandHandler> logger)
    {
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _gameNights = gameNights ?? throw new ArgumentNullException(nameof(gameNights));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CancelGameNightInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _gameNights.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.OrganizerUserId)
        {
            throw new ForbiddenException("Only the organizer can withdraw an invitation.");
        }

        // An invitation of another night is "not found" here, not "forbidden": the organizer owns this night,
        // and must not learn whether an id belongs to someone else's.
        var invitation = await _invitations.GetByIdAsync(command.InvitationId, cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.GameNightId != command.GameNightId)
        {
            throw new NotFoundException("GameNightInvitation", command.InvitationId.ToString());
        }

        // Withdrawing twice is not an error: the second request finds the work done.
        if (invitation.Status == GameNightInvitationStatus.Cancelled)
        {
            return;
        }

        try
        {
            invitation.Cancel(_timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            // Declined and expired invitations have no way out of their state: they are history.
            throw new ConflictException(
                $"An invitation that is {invitation.Status} is history and cannot be withdrawn.");
        }

        await _invitations.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await DropCachedPageAsync(invitation.Token, cancellationToken).ConfigureAwait(false);
    }

    private async Task DropCachedPageAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveByTagAsync(
                GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + token, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Best effort after a committed write: see the remarks of the handler
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not drop the cached invitation page after a withdrawal; it expires on its own");
        }
#pragma warning restore CA1031
    }
}
