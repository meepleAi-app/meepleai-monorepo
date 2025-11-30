using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for Disable2FACommand.
/// DDD: Thin wrapper - delegates to ITotpService for disable logic.
/// </summary>
public class Disable2FACommandHandler : ICommandHandler<Disable2FACommand, Disable2FAResult>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<Disable2FACommandHandler> _logger;

    public Disable2FACommandHandler(
        ITotpService totpService,
        ILogger<Disable2FACommandHandler> logger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Disable2FAResult> Handle(Disable2FACommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Delegate to existing TotpService (handles password + code verification)
            await _totpService.DisableTwoFactorAsync(command.UserId, command.CurrentPassword, command.TotpOrBackupCode, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("2FA disabled successfully for user {UserId} via CQRS", command.UserId);
            return new Disable2FAResult(Success: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "2FA disable failed for user {UserId}: Unauthorized", command.UserId);
            return new Disable2FAResult(Success: false, ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling 2FA for user {UserId}", command.UserId);
            return new Disable2FAResult(Success: false, ErrorMessage: "An error occurred");
        }
    }
}
