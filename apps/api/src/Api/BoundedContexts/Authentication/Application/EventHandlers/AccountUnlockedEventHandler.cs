using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Event handler for AccountUnlockedEvent.
/// Issue #3676: Creates audit log when account is unlocked (manual or automatic).
/// </summary>
internal sealed class AccountUnlockedEventHandler : INotificationHandler<AccountUnlockedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<AccountUnlockedEventHandler> _logger;

    public AccountUnlockedEventHandler(
        AuditService auditService,
        ILogger<AccountUnlockedEventHandler> logger)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AccountUnlockedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var unlockType = notification.WasManualUnlock ? "manual (admin)" : "automatic (successful login)";

            _logger.LogInformation(
                "Account unlocked for user {UserId}. Unlock type: {UnlockType}. Admin: {AdminId}",
                notification.UserId,
                unlockType,
                notification.UnlockedByAdminId?.ToString() ?? "N/A");

            // Create audit log entry
            await _auditService.LogAsync(
                userId: notification.UnlockedByAdminId?.ToString() ?? notification.UserId.ToString(),
                action: notification.WasManualUnlock ? "ACCOUNT_UNLOCKED_ADMIN" : "ACCOUNT_UNLOCKED_AUTO",
                resource: "User",
                resourceId: notification.UserId.ToString(),
                result: "Success",
                details: System.Text.Json.JsonSerializer.Serialize(new
                {
                    WasManualUnlock = notification.WasManualUnlock,
                    UnlockedByAdminId = notification.UnlockedByAdminId
                }),
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log but don't fail - audit is non-critical
            _logger.LogError(
                ex,
                "Failed to process account unlocked event for user {UserId}",
                notification.UserId);
        }
#pragma warning restore CA1031
    }
}
