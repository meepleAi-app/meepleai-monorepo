using System.Globalization;
using System.Text;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Helpers;
using Api.Infrastructure.Security;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Sends game night-related emails with responsive HTML templates via IEmailService.SendRawEmailAsync.
/// Issue #44/#47: Game night notification types and email templates.
/// Issue #4754: text in the organizer's language (<see cref="GameNightEmailTexts"/>), time written on the
/// night's own zone (<see cref="GameNightEmailTime"/>), and a footer that does not tell a guest, who has
/// no account, that they have one.
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
        GameNightEmailLocale locale,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(locale);

        var texts = GameNightEmailTexts.For(locale.Language);
        var formattedDate = FormatWhen(scheduledAt, locale, texts, title);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">{texts.LocationLabel}</td><td style="padding:8px 0;font-size:14px;font-weight:600;">{Encode(location)}</td></tr>"""
            : "";

        var gamesHtml = new StringBuilder();
        if (gameNames.Count > 0)
        {
            gamesHtml.Append(CultureInfo.InvariantCulture, $"""<tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">{texts.GamesLabel}</td><td style="padding:8px 0;font-size:14px;">""");
            foreach (var game in gameNames)
            {
                gamesHtml.Append(CultureInfo.InvariantCulture, $"""<span style="display:inline-block;background:#fef3c7;color:#92400e;padding:2px 10px;border-radius:12px;font-size:13px;margin:2px 4px 2px 0;">{Encode(game)}</span>""");
            }
            gamesHtml.Append("</td></tr>");
        }

        var subject = texts.InvitationSubject(title);
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#1f2937;">{texts.InvitationHeading}</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{texts.InvitationIntro(Encode(organizerName))}</p>

            <div style="background:#f9fafb;border-radius:12px;padding:20px;margin-bottom:24px;">
                <h2 style="margin:0 0 16px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <table style="width:100%;border-collapse:collapse;">
                    <tr><td style="padding:8px 0;color:#6b7280;font-size:14px;">{texts.WhenLabel}</td><td style="padding:8px 0;font-size:14px;font-weight:600;">{formattedDate}</td></tr>
                    {locationHtml}
                    {gamesHtml}
                </table>
            </div>

            <div style="text-align:center;margin-bottom:16px;">
                <a href="{Encode(rsvpAcceptUrl)}" style="display:inline-block;background:#059669;color:#ffffff;padding:12px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;margin-right:12px;">{texts.AcceptLabel}</a>
                <a href="{Encode(rsvpDeclineUrl)}" style="display:inline-block;background:#dc2626;color:#ffffff;padding:12px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;">{texts.DeclineLabel}</a>
            </div>
        """, GuestFooter(texts.InvitationGuestFooter(Encode(organizerName))), texts);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night invitation email sent to {Email} for event {Title}", DataMasking.MaskEmail(toEmail), LogSanitizer.Sanitize(title));
    }

    public async Task SendGameNightReminder24hEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        int confirmedCount,
        bool isPending,
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(locale);

        var texts = GameNightEmailTexts.For(locale.Language);
        var formattedDate = FormatWhen(scheduledAt, locale, texts, title);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.LocationLabel}: <strong>{Encode(location)}</strong></p>"""
            : "";

        var pendingCta = isPending
            ? $"""<div style="background:#fef3c7;border-radius:8px;padding:16px;margin-bottom:24px;text-align:center;"><p style="margin:0;color:#92400e;font-size:14px;font-weight:600;">{texts.ReminderPendingPrompt}</p></div>"""
            : "";

        var subject = texts.ReminderSubject(title);
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#1f2937;">{texts.ReminderHeading}</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{texts.ReminderIntro}</p>

            <div style="background:#f9fafb;border-radius:12px;padding:20px;margin-bottom:24px;">
                <h2 style="margin:0 0 12px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.WhenLabel}: <strong>{formattedDate}</strong></p>
                {locationHtml}
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.ConfirmedAttendeesLabel}: <strong>{confirmedCount}</strong></p>
            </div>

            {pendingCta}
        """, MemberFooter(texts, unsubscribeUrl), texts);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night 24h reminder email sent to {Email} for event {Title}", DataMasking.MaskEmail(toEmail), LogSanitizer.Sanitize(title));
    }

    public async Task SendGameNightCancelledEmailAsync(
        string toEmail,
        string organizerName,
        string title,
        DateTimeOffset scheduledAt,
        string unsubscribeUrl,
        GameNightEmailLocale locale,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(locale);

        var texts = GameNightEmailTexts.For(locale.Language);
        var formattedDate = FormatWhen(scheduledAt, locale, texts, title);

        var subject = texts.CancelledSubject(title);
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#dc2626;">{texts.CancelledHeading}</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{texts.CancelledIntro(Encode(organizerName))}</p>

            <div style="background:#fef2f2;border-radius:12px;padding:20px;margin-bottom:24px;border-left:4px solid #dc2626;">
                <h2 style="margin:0 0 8px;font-size:20px;color:#1f2937;text-decoration:line-through;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.WasScheduledForLabel}: <strong>{formattedDate}</strong></p>
            </div>

            <p style="color:#6b7280;font-size:14px;text-align:center;">{texts.CancelledClosing}</p>
        """, MemberFooter(texts, unsubscribeUrl), texts);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night cancelled email sent to {Email} for event {Title}", DataMasking.MaskEmail(toEmail), LogSanitizer.Sanitize(title));
    }

    public async Task SendGameNightRsvpConfirmationEmailAsync(
        string toEmail,
        string title,
        DateTimeOffset scheduledAt,
        string? location,
        GameNightEmailLocale locale,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(locale);

        var texts = GameNightEmailTexts.For(locale.Language);
        var formattedDate = FormatWhen(scheduledAt, locale, texts, title);
        var locationHtml = !string.IsNullOrWhiteSpace(location)
            ? $"""<p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.LocationLabel}: <strong>{Encode(location)}</strong></p>"""
            : "";

        var subject = texts.RsvpSubject(title);
        var body = WrapInLayout(subject, $"""
            <h1 style="margin:0 0 8px;font-size:24px;color:#059669;">{texts.RsvpHeading}</h1>
            <p style="margin:0 0 24px;color:#6b7280;font-size:16px;">{texts.RsvpIntro}</p>

            <div style="background:#ecfdf5;border-radius:12px;padding:20px;margin-bottom:24px;border-left:4px solid #059669;">
                <h2 style="margin:0 0 8px;font-size:20px;color:#1f2937;">{Encode(title)}</h2>
                <p style="margin:4px 0;font-size:14px;color:#6b7280;">{texts.WhenLabel}: <strong>{formattedDate}</strong></p>
                {locationHtml}
            </div>

            <p style="color:#6b7280;font-size:14px;text-align:center;">{texts.RsvpClosing}</p>
        """, GuestFooter(texts.RsvpGuestFooter), texts);

        await _emailService.SendRawEmailAsync(toEmail, subject, body, ct).ConfigureAwait(false);
        _logger.LogInformation("Game night RSVP confirmation email sent to {Email} for event {Title}", DataMasking.MaskEmail(toEmail), LogSanitizer.Sanitize(title));
    }

    /// <summary>
    /// The time the way the night's zone reads it, ready to drop into the HTML as it is
    /// (<see cref="GameNightEmailTime"/> encodes the one part that comes from a client, the zone id).
    /// When a zone was given but this host cannot use it, the time
    /// is in UTC and the email says so — the fallback is logged, because it is a configuration problem
    /// (no tzdata) or a client sending something odd, and either way someone should be able to find it.
    /// </summary>
    private string FormatWhen(DateTimeOffset scheduledAt, GameNightEmailLocale locale, GameNightEmailTexts texts, string title)
    {
        var formatted = GameNightEmailTime.FormatWhen(scheduledAt, locale.TimeZoneId, texts.IsItalian, out var zoneResolved);
        if (!zoneResolved)
        {
            _logger.LogWarning(
                "Game night {Title}: time zone {TimeZoneId} could not be resolved on this host; the email gives the time in UTC",
                LogSanitizer.Sanitize(title),
                LogSanitizer.Sanitize(locale.TimeZoneId));
        }

        return formatted;
    }

    /// <summary>
    /// Footer for an email that goes to an address and not to an account: it says why the guest got the email
    /// and nothing else. It carries no link — the unsubscribe link this footer used to hold pointed at a page
    /// that does not exist, and "manage notification preferences" promised a profile a guest does not have.
    /// <paramref name="reasonHtml"/> is HTML-ready: the fixed words are literal UTF-8 (the document says
    /// <c>charset=utf-8</c>, so "perché" needs no entity) and whatever came from outside is already encoded.
    /// </summary>
    private static string GuestFooter(string reasonHtml) =>
        $"""<p style="margin:0;font-size:12px;color:#9ca3af;">{reasonHtml}</p>""";

    /// <summary>Footer for an email whose recipient has a MeepleAI account and its notification preferences.</summary>
    private static string MemberFooter(GameNightEmailTexts texts, string unsubscribeUrl) =>
        $"""
        <p style="margin:0 0 8px;font-size:12px;color:#9ca3af;">{texts.MemberFooter}</p>
                            <a href="{Encode(unsubscribeUrl)}" style="font-size:12px;color:#6b7280;text-decoration:underline;">{texts.ManagePreferencesLabel}</a>
        """;

    /// <summary>
    /// Wraps email content in a responsive HTML layout with MeepleAI branding.
    /// </summary>
    private static string WrapInLayout(string title, string content, string footerHtml, GameNightEmailTexts texts)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="{texts.HtmlLang}">
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
                            {footerHtml}
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
