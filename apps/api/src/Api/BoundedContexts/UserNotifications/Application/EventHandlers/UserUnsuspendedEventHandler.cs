using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that sends notification when a user account is reactivated.
/// Dispatches via NotificationDispatcher for multi-channel delivery.
/// Issue #2886: Email notification for user unsuspension.
/// </summary>
internal sealed class UserUnsuspendedEventHandler : INotificationHandler<UserUnsuspendedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UserUnsuspendedEventHandler> _logger;

    public UserUnsuspendedEventHandler(
        INotificationDispatcher dispatcher,
        MeepleAiDbContext dbContext,
        ILogger<UserUnsuspendedEventHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UserUnsuspendedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Get user details
            var user = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for reactivation notification",
                    notification.UserId);
                return;
            }

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.CooldownEnded,
                RecipientUserId = notification.UserId,
                Payload = new GenericPayload(
                    "Account Reactivated",
                    "Your account has been reactivated. Welcome back!"),
                DeepLinkPath = "/dashboard"
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dispatched account reactivated notification to user {UserId}",
                notification.UserId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send account reactivated notification to user {UserId}",
                notification.UserId);
            // Don't rethrow - event handler failures should not block the main operation
        }
#pragma warning restore CA1031
    }
}
