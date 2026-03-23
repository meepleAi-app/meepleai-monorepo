using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Event handler for AccountLockedEvent.
/// Issue #3676: Sends email notification and creates audit log when account is locked.
/// </summary>
internal sealed class AccountLockedEventHandler : INotificationHandler<AccountLockedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly AuditService _auditService;
    private readonly ILogger<AccountLockedEventHandler> _logger;

    public AccountLockedEventHandler(
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        AuditService auditService,
        ILogger<AccountLockedEventHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AccountLockedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning(
                "Account locked for user {UserId} after {FailedAttempts} failed attempts. Locked until {LockedUntil}. IP: {IpAddress}",
                notification.UserId,
                notification.FailedAttempts,
                notification.LockedUntil,
                DataMasking.MaskIpAddress(notification.IpAddress));

            // Get user details for email
            var user = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for account locked notification",
                    notification.UserId);
                return;
            }

            // Send email notification first to know if it succeeded
            try
            {
                await _emailService.SendAccountLockedEmailAsync(
                    toEmail: user.Email,
                    userName: user.DisplayName ?? user.Email.Split('@')[0],
                    failedAttempts: notification.FailedAttempts,
                    lockedUntil: notification.LockedUntil,
                    ipAddress: notification.IpAddress,
                    ct: cancellationToken
                ).ConfigureAwait(false);

                _logger.LogInformation(
                    "Account locked notification sent to user {UserId}",
                    notification.UserId);

                // Create audit log entry for successful notification
                await _auditService.LogAsync(
                    userId: notification.UserId.ToString(),
                    action: "ACCOUNT_LOCKED",
                    resource: "User",
                    resourceId: notification.UserId.ToString(),
                    result: "Success",
                    details: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        FailedAttempts = notification.FailedAttempts,
                        LockedUntil = notification.LockedUntil,
                        IpAddress = notification.IpAddress,
                        EmailSent = true
                    }),
                    ipAddress: notification.IpAddress,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception emailEx)
            {
                // Log email failure separately in audit
                _logger.LogError(
                    emailEx,
                    "Failed to send account locked email to user {UserId}",
                    notification.UserId);

                await _auditService.LogAsync(
                    userId: notification.UserId.ToString(),
                    action: "ACCOUNT_LOCKED_EMAIL_FAILED",
                    resource: "User",
                    resourceId: notification.UserId.ToString(),
                    result: "Failed",
                    details: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        FailedAttempts = notification.FailedAttempts,
                        LockedUntil = notification.LockedUntil,
                        IpAddress = notification.IpAddress,
                        EmailSent = false,
                        Error = emailEx.Message
                    }),
                    ipAddress: notification.IpAddress,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log but don't fail - notification is non-critical
            _logger.LogError(
                ex,
                "Failed to process account locked event for user {UserId}",
                notification.UserId);
        }
#pragma warning restore CA1031
    }
}
