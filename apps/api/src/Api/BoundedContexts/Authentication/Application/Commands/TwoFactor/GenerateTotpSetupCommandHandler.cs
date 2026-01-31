using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Handles TOTP setup generation with CQRS pattern.
/// Business logic: User validation → Secret generation → QR code creation → Backup codes generation.
/// Infrastructure delegation: TOTP crypto operations via ITotpService.
/// </summary>
internal sealed class GenerateTotpSetupCommandHandler : ICommandHandler<GenerateTotpSetupCommand, GenerateTotpSetupResult>
{
    private readonly ITotpService _totpService;
    private readonly ILogger<GenerateTotpSetupCommandHandler> _logger;

    public GenerateTotpSetupCommandHandler(
        ITotpService totpService,
        ILogger<GenerateTotpSetupCommandHandler> logger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerateTotpSetupResult> Handle(GenerateTotpSetupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Business logic validation
        if (command.UserId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.UserEmail))
        {
            throw new ArgumentException("User email is required", nameof(command));
        }

        // Delegate TOTP setup to infrastructure service
        var setup = await _totpService.GenerateSetupAsync(command.UserId, command.UserEmail, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("TOTP setup generated for user {UserId}", command.UserId);

        return new GenerateTotpSetupResult
        {
            Secret = setup.Secret,
            QrCodeUrl = setup.QrCodeUrl,
            BackupCodes = setup.BackupCodes
        };
    }
}
