using System.Globalization;
using System.Text;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Sends game night-related emails with responsive HTML templates via IEmailService.SendRawEmailAsync.
/// Issue #44/#47: Game night notification types and email templates.
/// </summary>
internal sealed class GameNightEmailService : IGameNightEmailService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<GameNightEmailService> _logger;

    public GameNightEmailService(IEmailService emailService, ILogger<GameNightEmailService> logger)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendGameNightInvitationEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        IReadOnlyList<string> gameNames,
        string rsvpAcceptUrl,
        string rsvpDeclineUrl,
        string unsubscribeUrl,
        CancellationToken ct = default)
    {
        var formattedDate = scheduledAt.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">Location</td><td style="padding:8px 0;font-size:14px;font-weight:600;">{Encode(location)}</td></tr>"""
            : "";

        var gamesHtml = new StringBuilder();
        if (gameNames.Count > 0)
        {
            gamesHtml.Append("""<tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">Games</td><td style="padding:8px 0;font-size:14px;">""");
            foreach (var game in gameNames)
            {
                gamesHtml.Append(CultureInfo.InvariantCulture, $"""<span style="display:inline-block;background:#fef3c7;color:#92400e;padding:2px 10px;border-radius:12px;font-size:13px;margin:2px 4px 2px 0;">{Encode(game)}</span>""");
            }
            gamesHtml.Append("</td></tr>");
        }

        var subject = $"You're Invited: {title} - MeepleAI";
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#1f2937;">Game Night Invitation</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{Encode(organizerName)} has invited you to a game night!</p>

            <div style="background:#f9fafb;border-radius:12px;padding:20px;margin-bottom:24px;">
                <h2 style="margin:0 0 16px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <table style="width:100%;border-collapse:collapse;">
                    <tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">When</td><td style="padding:8px 0;font-size:14px;font-weight:600;">{formattedDate}</td></tr>
                    {locationHtml}
                    {gamesHtml}
                </table>
            </div>

            <div style="text-align:center;margin-bottom:16px;">
                <a href="{Encode(rsvpAcceptUrl)}" style="display:inline-block;background:#059669;color:#ffffff;padding:12px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;margin-right:12px;">Accept</a>
                <a href="{Encode(rsvpDeclineUrl)}" style="display:inline-block;background:#dc2626;color:#ffffff;padding:12px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;">Decline</a>
            </div>
        """, unsubscribeUrl);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night invitation email sent to {Email} for event {Title}", toEmail, title);
    }

    public async Task SendGameNightReminder24hEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        int confirmedCount,
        bool isPending,
        string unsubscribeUrl,
        CancellationToken ct = default)
    {
        var formattedDate = scheduledAt.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<p style="margin:4px 0;font-size:14px;color:#6b7280;">Location: <strong>{Encode(location)}</strong></p>"""
            : "";

        var pendingCta = isPending
            ? """<div style="background:#fef3c7;border-radius:8px;padding:16px;margin-bottom:24px;text-align:center;"><p style="margin:0;color:#92400e;font-size:14px;font-weight:600;">You haven't responded yet. Will you be joining?</p></div>"""
            : "";

        var subject = $"Reminder: {title} is tomorrow! - MeepleAI";
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#1f2937;">Game Night Tomorrow!</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">Your game night is coming up in 24 hours.</p>

            <div style="background:#f9fafb;border-radius:12px;padding:20px;margin-bottom:24px;">
                <h2 style="margin:0 0 12px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">When: <strong>{formattedDate}</strong></p>
                {locationHtml}
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">Confirmed attendees: <strong>{confirmedCount}</strong></p>
            </div>

            {pendingCta}
        """, unsubscribeUrl);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night 24h reminder email sent to {Email} for event {Title}", toEmail, title);
    }

    public async Task SendGameNightCancelledEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string unsubscribeUrl,
        CancellationToken ct = default)
    {
        var formattedDate = scheduledAt.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);

        var subject = $"Cancelled: {title} - MeepleAI";
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#dc2626;">Game Night Cancelled</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{Encode(organizerName)} has cancelled the following game night.</p>

            <div style="background:#fef2f2;border-radius:12px;padding:20px;margin-bottom:24px;border-left:4px solid #dc2626;">
                <h2 style="margin:0 0 8px;font-size:20px;color:#1f2937;text-decoration:line-through;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">Was scheduled for: <strong>{formattedDate}</strong></p>
            </div>

            <p style="color:#6b7280;font-size:14px;text-align:center;">We hope to see you at the next one!</p>
        """, unsubscribeUrl);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night cancelled email sent to {Email} for event {Title}", toEmail, title);
    }

    public async Task SendGameNightRsvpConfirmationEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        string unsubscribeUrl,
        CancellationToken ct = default)
    {
        var formattedDate = scheduledAt.ToString("dddd, MMMM d, yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<p style="margin:4px 0;font-size:14px;color:#6b7280;">Location: <strong>{Encode(location)}</strong></p>"""
            : "";

        var subject = $"RSVP Confirmed: {title} - MeepleAI";
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#059669;">You're In!</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">Your RSVP has been confirmed for the game night.</p>

            <div style="background:#ecfdf5;border-radius:12px;padding:20px;margin-bottom:24px;border-left:4px solid #059669;">
                <h2 style="margin:0 0 8px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">When: <strong>{formattedDate}</strong></p>
                {locationHtml}
            </div>

            <p style="color:#6b7280;font-size:14px;text-align:center;">See you there!</p>
        """, unsubscribeUrl);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night RSVP confirmation email sent to {Email} for event {Title}", toEmail, title);
    }

    /// <summary>
    /// Wraps email content in a responsive HTML layout with MeepleAI branding.
    /// </summary>
    private static string WrapInLayout(string title, string content, string unsubscribeUrl)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
            <title>{Encode(title)}</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;">
            <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;background-color:#f3f4f6;">
                <tr><td style="padding:32px 16px;">
                    <table role="presentation" cellpadding="0" cellspacing="0" style="max-width:600px;margin:0 auto;width:100%;">
                        <!-- Header -->
                        <tr><td style="background:linear-gradient(135deg,#f59e0b,#d97706);padding:24px 32px;border-radius:12px 12px 0 0;text-align:center;">
                            <span style="font-size:28px;">&#127922;</span>
                            <span style="color:#ffffff;font-size:20px;font-weight:700;vertical-align:middle;margin-left:8px;">MeepleAI</span>
                        </td></tr>
                        <!-- Body -->
                        <tr><td style="background:#ffffff;padding:32px;border-radius:0 0 12px 12px;">
                            {content}
                        </td></tr>
                        <!-- Footer -->
                        <tr><td style="padding:24px 32px;text-align:center;">
                            <p style="margin:0 0 8px;font-size:12px;color:#9ca3af;">You received this email because you have a MeepleAI account.</p>
                            <a href="{Encode(unsubscribeUrl)}" style="font-size:12px;color:#6b7280;text-decoration:underline;">Manage notification preferences</a>
                        </td></tr>
                    </table>
                </td></tr>
            </table>
        </body>
        </html>
        """;
    }

    /// <summary>
    /// HTML-encodes a string for safe embedding in HTML attributes and content.
    /// </summary>
    private static string Encode(string? value)
    {
        return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
