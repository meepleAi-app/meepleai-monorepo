using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that sends email notification when a user account is reactivated.
/// Issue #2886: Email notification for user unsuspension.
/// </summary>
internal sealed class UserUnsuspendedEventHandler : INotificationHandler<UserUnsuspendedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserUnsuspendedEventHandler> _logger;

    public UserUnsuspendedEventHandler(
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        ILogger<UserUnsuspendedEventHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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

            // Send email notification
            await _emailService.SendAccountReactivatedEmailAsync(
                toEmail: user.Email,
                userName: user.DisplayName ?? user.Email,
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Account reactivated notification sent to user {UserId}",
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
