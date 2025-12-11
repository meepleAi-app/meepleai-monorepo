using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for Enable2FACommand.
/// DDD: Thin wrapper around ITotpService (infrastructure handles crypto logic).
/// Uses existing TotpService.EnableTwoFactorAsync which already implements all business logic.
/// </summary>
public class Enable2FACommandHandler : ICommandHandler<Enable2FACommand, Enable2FAResult>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<Enable2FACommandHandler> _logger;

    public Enable2FACommandHandler(
        ITotpService totpService,
        ILogger<Enable2FACommandHandler> logger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Enable2FAResult> Handle(Enable2FACommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Delegate to existing TotpService (handles all logic: verification, domain updates, persistence)
            var result = await _totpService.EnableTwoFactorAsync(command.UserId, command.TotpCode, cancellationToken).ConfigureAwait(false);

            if (result)
            {
                _logger.LogInformation("2FA enabled successfully for user {UserId} via CQRS", command.UserId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling 2FA for user {UserId}", command.UserId);
            return new Enable2FAResult(Success: false, ErrorMessage: "An error occurred while enabling two-factor authentication");
        }
    }
}
