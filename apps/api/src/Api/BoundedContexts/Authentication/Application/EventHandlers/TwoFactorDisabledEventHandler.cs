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
public sealed class TwoFactorDisabledEventHandler : DomainEventHandlerBase<TwoFactorDisabledEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public TwoFactorDisabledEventHandler(
        MeepleAiDbContext dbContext,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<DomainEventHandlerBase<TwoFactorDisabledEvent>> logger)
        : base(dbContext, logger)
    {
        _userRepository = userRepository;
        _emailService = emailService;
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
                    cancellationToken);

                Logger.LogInformation(
                    "2FA disabled email sent to user {UserId} (Admin override: {AdminOverride})",
                    domainEvent.UserId,
                    domainEvent.WasAdminOverride);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the event handler - email is non-critical
            Logger.LogError(
                ex,
                "Failed to send 2FA disabled email to user {UserId}",
                domainEvent.UserId);
        }
    }

    protected override Guid? GetUserId(TwoFactorDisabledEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(TwoFactorDisabledEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["UserId"] = domainEvent.UserId,
            ["Action"] = "TwoFactorDisabled",
            ["WasAdminOverride"] = domainEvent.WasAdminOverride
        };
    }
}
