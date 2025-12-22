using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the TwoFactorDisabledEvent domain event.
/// Creates audit log entry automatically via base class and sends email notification.
/// </summary>
internal sealed class TwoFactorDisabledEventHandler : DomainEventHandlerBase<TwoFactorDisabledEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public TwoFactorDisabledEventHandler(
        MeepleAiDbContext dbContext,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<TwoFactorDisabledEventHandler> logger)
        : base(dbContext, logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    protected override async Task HandleEventAsync(TwoFactorDisabledEvent domainEvent, CancellationToken cancellationToken)
    {
        // Send security alert email to user
        try
        {
            var user = await _userRepository.GetByIdAsync(domainEvent.UserId, cancellationToken).ConfigureAwait(false);
            if (user != null)
            {
                await _emailService.SendTwoFactorDisabledEmailAsync(
                    user.Email,
                    user.DisplayName,
                    domainEvent.WasAdminOverride,
                    cancellationToken).ConfigureAwait(false);

                Logger.LogInformation(
                    "2FA disabled email sent to user {UserId} (Admin override: {AdminOverride})",
                    domainEvent.UserId,
                    domainEvent.WasAdminOverride);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator/event pattern).
        // Errors logged for monitoring; failed email delivery doesn't block 2FA operations.
        catch (Exception ex)
        {
            // Log error but don't fail the event handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send 2FA disabled email to user {UserId}",
                domainEvent.UserId);
        }
#pragma warning restore CA1031
    }

    protected override Guid? GetUserId(TwoFactorDisabledEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(TwoFactorDisabledEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["UserId"] = domainEvent.UserId,
            ["Action"] = "TwoFactorDisabled",
            ["WasAdminOverride"] = domainEvent.WasAdminOverride
        };
    }
}
