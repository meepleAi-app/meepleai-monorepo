using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;

using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// #535 ME-M3.3: fans out an admin in-app notification (with per-admin opt-in email) when a mechanic card
/// is suppressed (auto #534 or manual). Auto-discovered by MediatR; dispatched post-commit via the
/// domain-event outbox. In-app is created for every admin regardless of preferences; email is gated by
/// <c>NotificationPreferences.EmailOnCardSuppressed</c> inside the dispatcher.
/// </summary>
internal sealed class MechanicCardSuppressedAdminNotificationHandler
    : DomainEventHandlerBase<MechanicCardSuppressedEvent>
{
    // Metrics (#532) + re-process queue (#534) routes are not built yet; the dashboard is the closest
    // real target and links to per-game comprehension metrics.
    private const string DeepLink = NotificationRoutes.AdminMechanicExtractorDashboard;

    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public MechanicCardSuppressedAdminNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<MechanicCardSuppressedAdminNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(
        MechanicCardSuppressedEvent domainEvent, CancellationToken cancellationToken)
    {
        var game = await _sharedGameRepository
            .GetByIdAsync(domainEvent.SharedGameId, cancellationToken)
            .ConfigureAwait(false);
        var title = string.IsNullOrWhiteSpace(game?.Title) ? "un gioco" : game!.Title;

        var admins = await _userRepository.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var admin in admins)
        {
            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AdminMechanicCardSuppressed,
                RecipientUserId = admin.Id,
                Payload = new GenericPayload(
                    "[Admin] Scheda Meccanica Soppressa",
                    $"La scheda meccaniche di «{title}» è stata soppressa. Motivo: {domainEvent.Reason}"),
                DeepLinkPath = DeepLink,
                SourceEventId = domainEvent.EventId
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
