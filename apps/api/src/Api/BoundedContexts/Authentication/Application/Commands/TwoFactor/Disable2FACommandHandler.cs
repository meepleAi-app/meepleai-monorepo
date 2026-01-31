using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for Disable2FACommand.
/// DDD: Thin wrapper - delegates to ITotpService for disable logic.
/// </summary>
internal class Disable2FACommandHandler : ICommandHandler<Disable2FACommand, Disable2FAResult>
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
        ArgumentNullException.ThrowIfNull(command);
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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (UnauthorizedAccessException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result pattern.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling 2FA for user {UserId}", command.UserId);
            return new Disable2FAResult(Success: false, ErrorMessage: "An error occurred");
        }
#pragma warning restore CA1031
    }
}
