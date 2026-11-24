using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.BoundedContexts.SecurityAudit.Application.Services;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for Enable2FACommand.
/// DDD: Thin wrapper around ITotpService (infrastructure handles crypto logic).
/// Uses existing TotpService.EnableTwoFactorAsync which already implements all business logic.
/// </summary>
internal class Enable2FACommandHandler : ICommandHandler<Enable2FACommand, Enable2FAResult>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<Enable2FACommandHandler> _logger;
    private readonly IAuditLogger _auditLogger;

    public Enable2FACommandHandler(
        ITotpService totpService,
        ILogger<Enable2FACommandHandler> logger,
        IAuditLogger auditLogger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<Enable2FAResult> Handle(Enable2FACommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Delegate to existing TotpService (handles all logic: verification, domain updates, persistence)
            var result = await _totpService.EnableTwoFactorAsync(command.UserId, command.TotpCode, cancellationToken).ConfigureAwait(false);

            if (result)
            {
                _logger.LogInformation("2FA enabled successfully for user {UserId} via CQRS", command.UserId);

                // #3838
                await _auditLogger.LogAsync(
                    AuditEventType.TwoFactorEnabled,
                    actorUserId: command.UserId,
                    targetUserId: command.UserId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Note: Backup codes were already generated during GenerateSetupAsync
                // Return success without codes (user should have saved them during setup)
                return new Enable2FAResult(Success: true);
            }
            else
            {
                _logger.LogWarning("2FA enable failed for user {UserId}: Invalid code", command.UserId);
                return new Enable2FAResult(Success: false, ErrorMessage: "Invalid verification code");
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result pattern.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling 2FA for user {UserId}", command.UserId);
            return new Enable2FAResult(Success: false, ErrorMessage: "An error occurred while enabling two-factor authentication");
        }
#pragma warning restore CA1031
    }
}
