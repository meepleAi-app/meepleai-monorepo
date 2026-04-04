using System.Globalization;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for SendTestEmailCommand.
/// Sends a test email immediately bypassing the queue.
/// Issue #39: Admin email management.
/// </summary>
internal class SendTestEmailCommandHandler : ICommandHandler<SendTestEmailCommand, bool>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendTestEmailCommandHandler> _logger;

    public SendTestEmailCommandHandler(
        IEmailService emailService,
        ILogger<SendTestEmailCommandHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<bool> Handle(SendTestEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var htmlBody = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h1 style="font-size: 24px; color: #333;">&#127922; MeepleAI Test Email</h1>
                <p style="color: #666; line-height: 1.6;">
                    This is a test email from the MeepleAI admin panel.<br>
                    If you received this, your email configuration is working correctly.
                </p>
                <p style="font-size: 12px; color: #999; margin-top: 32px;">
                    Sent at: {timestamp} UTC
                </p>
            </div>
            """;

            await _emailService.SendRawEmailAsync(
                command.To,
                "MeepleAI Test Email",
                htmlBody,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Admin test email sent to {To}", command.To);
            return true;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to send test email to {To}", command.To);
            return false;
        }
    }
}
