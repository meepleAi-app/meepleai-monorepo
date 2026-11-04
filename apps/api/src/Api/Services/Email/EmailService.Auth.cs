using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;
using Api.Services.Email;

namespace Api.Services;

internal partial class EmailService
{
    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken,
        string? language,
        CancellationToken ct = default)
    {
        try
        {
            var texts = AuthEmailTexts.For(language);
            var resetUrl = BuildPasswordResetUrl(resetToken);
            var subject = texts.ResetSubject;
            var body = BuildPasswordResetEmailBody(texts, toName, resetUrl, _timeProvider.GetUtcNow().Year);

            await SendViaTransportAsync(toEmail, subject, body, "password_reset", ct, toName).ConfigureAwait(false);

            _logger.LogInformation(
                "Password reset email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send password reset email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send password reset email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendTwoFactorDisabledEmailAsync(
        string toEmail,
        string toName,
        bool wasAdminOverride,
        string? language,
        CancellationToken ct = default)
    {
        try
        {
            var texts = AuthEmailTexts.For(language);
            var subject = texts.TwoFactorSubject;
            var body = BuildTwoFactorDisabledEmailBody(texts, toName, wasAdminOverride, _timeProvider.GetUtcNow().Year);

            await SendViaTransportAsync(toEmail, subject, body, "two_factor_disabled", ct, toName).ConfigureAwait(false);

            _logger.LogInformation(
                "Two-factor authentication disabled email sent successfully to {Email} (Admin override: {AdminOverride})",
                DataMasking.MaskEmail(toEmail),
                wasAdminOverride);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send two-factor authentication disabled email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send two-factor authentication disabled email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-3071: Email verification
    public async Task SendVerificationEmailAsync(
        string toEmail,
        string toName,
        string verificationToken,
        string? language,
        CancellationToken ct = default)
    {
        try
        {
            var verifyUrl = $"{FrontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
            var texts = AuthEmailTexts.For(language);
            var subject = texts.VerifySubject;
            var body = BuildVerificationEmailBody(texts, toName, verifyUrl, _timeProvider.GetUtcNow().Year);

            await SendViaTransportAsync(toEmail, subject, body, "email_verification", ct, toName).ConfigureAwait(false);

            _logger.LogInformation(
                "Verification email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send verification email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send verification email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-2886: User suspension notification emails
    public async Task SendAccountSuspendedEmailAsync(
        string toEmail,
        string userName,
        string? reason,
        string? language,
        CancellationToken ct = default)
    {
        try
        {
            var texts = AuthEmailTexts.For(language);
            var subject = texts.SuspendedSubject;
            var body = BuildAccountSuspendedEmailBody(texts, userName, reason, _timeProvider.GetUtcNow().Year);

            await SendViaTransportAsync(toEmail, subject, body, "account_suspended", ct, userName).ConfigureAwait(false);

            _logger.LogInformation(
                "Account suspended email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send account suspended email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send account suspended email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendAccountReactivatedEmailAsync(
        string toEmail,
        string userName,
        string? language,
        CancellationToken ct = default)
    {
        try
        {
            var texts = AuthEmailTexts.For(language);
            var subject = texts.ReactivatedSubject;
            var body = BuildAccountReactivatedEmailBody(texts, userName, _timeProvider.GetUtcNow().Year);

            await SendViaTransportAsync(toEmail, subject, body, "account_reactivated", ct, userName).ConfigureAwait(false);

            _logger.LogInformation(
                "Account reactivated email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send account reactivated email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send account reactivated email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-3676: Account lockout notification
    public async Task SendAccountLockedEmailAsync(
        string toEmail,
        string userName,
        int failedAttempts,
        DateTime lockedUntil,
        string? ipAddress,
        string? language,
        CancellationToken ct = default)
    {
        // Ensure lockedUntil is UTC for consistent timezone display
        if (lockedUntil.Kind != DateTimeKind.Utc)
        {
            _logger.LogWarning(
                "lockedUntil provided with Kind={Kind}, converting to UTC",
                lockedUntil.Kind);
            lockedUntil = lockedUntil.ToUniversalTime();
        }

        try
        {
            var texts = AuthEmailTexts.For(language);
            var subject = texts.LockedSubject;
            var now = _timeProvider.GetUtcNow();
            var body = BuildAccountLockedEmailBody(texts, userName, failedAttempts, lockedUntil, ipAddress, now.UtcDateTime, now.Year);

            await SendViaTransportAsync(toEmail, subject, body, "account_locked", ct, userName).ConfigureAwait(false);

            _logger.LogInformation(
                "Account locked email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send account locked email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send account locked email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-124: Invitation system emails
    // Issue #1629: refactored to route through IEmailSender (Resend/SMTP).
    // Redeem link: the base-invitation flow (token only, no pending user) is redeemed at
    // /accept-invite?token= → POST /auth/accept-invitation, which creates the user from
    // token+password (AcceptInvitationCommandHandler). NOTE: #1629 mistakenly repointed this
    // to /invites/{token}, which is the game-night RSVP landing (game_night_invitations) and
    // 404s for invitation tokens; restored to /accept-invite here. (The enhanced/provisioned
    // flow with a pending user uses /setup-account?token= — see the overload below.)
    public async Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string token,
        string invitedByName,
        CancellationToken ct = default)
    {
        try
        {
            var inviteLink = $"{FrontendBaseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";
            var subject = "You've been invited to MeepleAI";
            var body = BuildInvitationEmailBody(role, inviteLink, invitedByName);

            await SendViaTransportAsync(toEmail, subject, body, "invitation", ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Invitation email sent successfully to {Email} for role {Role}",
                DataMasking.MaskEmail(toEmail), role);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send invitation email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send invitation email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-124: Enhanced invitation email with custom message, platform intro, and expiry notice
    // Issue #1629: same transport routing + link fix as the basic overload above.
    // The enhanced flow points at /setup-account (account-provisioning UI), not /invites/[token].
    public async Task SendInvitationEmailAsync(
        string toEmail,
        string displayName,
        string role,
        string token,
        string invitedByName,
        string? customMessage,
        DateTime expiresAt,
        CancellationToken ct = default)
    {
        try
        {
            var setupLink = $"{FrontendBaseUrl}/setup-account?token={Uri.EscapeDataString(token)}";
            var subject = "Sei stato invitato su MeepleAI!";
            var body = BuildEnhancedInvitationEmailBody(displayName, role, setupLink, invitedByName, customMessage, expiresAt);

            await SendViaTransportAsync(toEmail, subject, body, "invitation", ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Enhanced invitation email sent successfully to {Email} for role {Role}",
                DataMasking.MaskEmail(toEmail), role);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send enhanced invitation email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send invitation email", ex);
        }
#pragma warning restore CA1031
    }

    // Access request rejection notification
    public async Task SendAccessRequestRejectedEmailAsync(
        string toEmail,
        string? reason,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "MeepleAI — Access Request Update";
            var body = BuildAccessRequestRejectedEmailBody(reason);

            await SendViaTransportAsync(toEmail, subject, body, "access_request_rejected", ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Access request rejected email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
        // External service integration requires catching all SMTP exceptions to provide consistent error handling
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send access request rejected email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send access request rejected email", ex);
        }
#pragma warning restore CA1031
    }

    // ===== Template builders =====
    //
    // #4812: the account emails (reset, verification, two-factor disabled, suspended, reactivated, locked) take
    // their words from AuthEmailTexts, in the recipient's language. The markup is the same for both languages;
    // only the text differs, and only the text is in AuthEmailTexts.

    // #4754: the footer year was the literal 2025. `internal` so a test can pin the year without a mail transport:
    // this method sends through SmtpClient directly, not through IEmailSender.
    internal static string BuildVerificationEmailBody(AuthEmailTexts t, string userName, string verifyUrl, int year)
    {
        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.VerifyTitle}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">{t.VerifyBannerHeading}</h2>
        <p style=""margin: 0; color: #155724;"">{t.VerifyBannerText}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>{t.Greeting(userName)}</p>

        <p>{t.VerifyBody}</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{verifyUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">{t.VerifyButton}</a>
        </div>

        <p>{t.CopyLinkPrompt}</p>
        <p style=""word-break: break-all; color: #3498db;"">{verifyUrl}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.VerifyExpiry}
        </p>

        <p style=""font-size: 14px; color: #666;"">
            {t.VerifyIgnore}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private static string BuildPasswordResetEmailBody(AuthEmailTexts t, string userName, string resetUrl, int year)
    {
        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.ResetTitle}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">{t.ResetHeading}</h2>

        <p>{t.Greeting(userName)}</p>

        <p>{t.ResetIntro}</p>

        <p>{t.ResetPrompt}</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{resetUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">{t.ResetButton}</a>
        </div>

        <p>{t.CopyLinkPrompt}</p>
        <p style=""word-break: break-all; color: #3498db;"">{resetUrl}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.ResetExpiry}
        </p>

        <p style=""font-size: 14px; color: #666;"">
            {t.ResetSupport}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private static string BuildTwoFactorDisabledEmailBody(AuthEmailTexts t, string userName, bool wasAdminOverride, int year)
    {
        var reason = wasAdminOverride ? t.TwoFactorReasonByAdmin : t.TwoFactorReasonBySelf;
        var warning = wasAdminOverride ? t.TwoFactorWarningByAdmin : t.TwoFactorWarningBySelf;

        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.TwoFactorSubject}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">{t.TwoFactorBannerHeading}</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">{t.TwoFactorSubject}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>{t.Greeting(userName)}</p>

        <p>{reason}</p>

        <p style=""margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <strong>{t.TwoFactorImportantLabel}</strong> {warning}
        </p>

        <p style=""margin-top: 30px;"">{t.TwoFactorReenablePrompt}</p>
        <ol>
            <li>{t.TwoFactorReenableStepLogin}</li>
            <li>{t.TwoFactorReenableStepSettings}</li>
            <li>{t.TwoFactorReenableStepSetup}</li>
        </ol>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.TwoFactorNotice}
        </p>

        <p style=""font-size: 14px; color: #666;"">
            {t.TwoFactorQuestions}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedSecurityFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountSuspendedEmailBody(AuthEmailTexts t, string userName, string? reason, int year)
    {
        var reasonSection = !string.IsNullOrWhiteSpace(reason)
            ? $@"
        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>{t.SuspendedReasonLabel}</strong></p>
            <p style=""margin: 10px 0;"">{reason}</p>
        </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.SuspendedTitle}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">{t.SuspendedBannerHeading}</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">{t.SuspendedBannerText}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>{t.Greeting(userName)}</p>

        <p>{t.SuspendedBody}</p>
{reasonSection}
        <p>{t.SuspendedAppeal}</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""mailto:support@meepleai.dev"" style=""background-color: #dc3545; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">{t.SuspendedContactButton}</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.SuspendedClosing}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountReactivatedEmailBody(AuthEmailTexts t, string userName, int year)
    {
        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.ReactivatedTitle}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">{t.ReactivatedBannerHeading}</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{t.ReactivatedBannerText}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>{t.Greeting(userName)}</p>

        <p>{t.ReactivatedBody}</p>

        <p>{t.ReactivatedWelcome}</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #d1ecf1; border-left: 4px solid #17a2b8; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>{t.ReactivatedNextStepsLabel}</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>{t.ReactivatedStepLogin}</li>
                <li>{t.ReactivatedStepGuidelines}</li>
                <li>{t.ReactivatedStepEnjoy}</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.ReactivatedClosing}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountLockedEmailBody(AuthEmailTexts t, string userName, int failedAttempts, DateTime lockedUntil, string? ipAddress, DateTime now, int year)
    {
        // `now` is the injected clock's, like the footer year: the length of the lock is counted from the same
        // instant the email is composed at, not from the machine's clock.
        var lockDurationMinutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
        var unlocksAt = lockedUntil.ToString(t.LockedUnlocksAtFormat, CultureInfo.InvariantCulture);
        var ipSection = !string.IsNullOrWhiteSpace(ipAddress)
            ? $"<li>{t.LockedIpItem(ipAddress)}</li>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html lang=""{t.HtmlLang}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{t.LockedTitle}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">{t.LockedBannerHeading}</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">{t.LockedBannerText}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>{t.Greeting(userName)}</p>

        <p>{t.LockedBody(failedAttempts)}</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #6c757d; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>{t.LockedDetailsLabel}</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>{t.LockedFailedAttemptsItem(failedAttempts)}</li>
                <li>{t.LockedDurationItem(lockDurationMinutes)}</li>
                <li>{t.LockedUnlocksAtItem(unlocksAt)}</li>
                {ipSection}
            </ul>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #d1ecf1; border-left: 4px solid #17a2b8; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>{t.LockedWhatToDoLabel}</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>{t.LockedStepWait}</li>
                <li>{t.LockedStepPassword}</li>
                <li>{t.LockedStepForgotPassword}</li>
            </ul>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8d7da; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>{t.LockedSecurityLabel}</strong></p>
            <p style=""margin: 10px 0;"">{t.LockedSecurityIntro}</p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>{t.LockedAdviceChangePassword}</li>
                <li>{t.LockedAdviceTwoFactor}</li>
                <li>{t.LockedAdviceReviewActivity}</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            {t.LockedSupport}
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>{t.AutomatedSecurityFooter}</p>
        <p>{t.Copyright(year)}</p>
    </div>
</body>
</html>
";
    }

    private string BuildInvitationEmailBody(string role, string inviteLink, string invitedByName)
    {
        var roleDisplay = string.IsNullOrWhiteSpace(role) ? "user" : role;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>You've been invited to MeepleAI</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">You've Been Invited!</h2>

        <p><strong>{invitedByName}</strong> has invited you to join MeepleAI as a <strong>{roleDisplay}</strong>.</p>

        <p>MeepleAI is an AI-powered board game assistant that helps you manage your collection, learn rules, and enhance your gaming sessions.</p>

        <p>Click the button below to accept your invitation and create your account:</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{inviteLink}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Accept Invitation</a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #3498db;"">{inviteLink}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This invitation link will expire in 7 days for security reasons. If it expires, ask your administrator to resend the invitation.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you did not expect this invitation, you can safely ignore this email.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; {FooterYear} MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private string BuildEnhancedInvitationEmailBody(
        string displayName,
        string role,
        string setupLink,
        string invitedByName,
        string? customMessage,
        DateTime expiresAt)
    {
        var roleDisplay = string.IsNullOrWhiteSpace(role) ? "utente" : role;
        var safeDisplayName = System.Net.WebUtility.HtmlEncode(displayName);
        var safeInvitedByName = System.Net.WebUtility.HtmlEncode(invitedByName);
        var expiryFormatted = expiresAt.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("it-IT"));

        var customMessageBlock = string.Empty;
        if (!string.IsNullOrWhiteSpace(customMessage))
        {
            var safeMessage = System.Net.WebUtility.HtmlEncode(customMessage);
            customMessageBlock = $@"
        <div style=""background-color: #f0f4f8; border-left: 4px solid #3498db; padding: 15px 20px; margin: 20px 0; border-radius: 0 5px 5px 0;"">
            <p style=""margin: 0 0 8px 0; font-size: 13px; color: #666; font-weight: bold;"">{safeInvitedByName} dice:</p>
            <p style=""margin: 0; font-style: italic; color: #444;"">&ldquo;{safeMessage}&rdquo;</p>
        </div>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Sei stato invitato su MeepleAI!</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">Ciao {safeDisplayName}!</h2>

        <p><strong>{safeInvitedByName}</strong> ti ha invitato a unirti a MeepleAI come <strong>{roleDisplay}</strong>.</p>
{customMessageBlock}
        <p style=""color: #555;"">MeepleAI &egrave; il tuo assistente AI per giochi da tavolo. Gestisci la tua collezione, ottieni risposte dalle regole dei tuoi giochi, e scopri nuovi titoli.</p>

        <p>Clicca il pulsante qui sotto per configurare il tuo account:</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{setupLink}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Configura il tuo account</a>
        </div>

        <p>Oppure copia e incolla questo link nel tuo browser:</p>
        <p style=""word-break: break-all; color: #3498db;"">{setupLink}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Questo invito scade il <strong>{expiryFormatted}</strong>. Se scade, chiedi al tuo amministratore di inviarne uno nuovo.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            Se non ti aspettavi questo invito, puoi ignorare questa email.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>Questo &egrave; un messaggio automatico, non rispondere a questa email.</p>
        <p>&copy; {FooterYear} MeepleAI. Tutti i diritti riservati.</p>
    </div>
</body>
</html>
";
    }

    private string BuildAccessRequestRejectedEmailBody(string? reason)
    {
        var reasonSection = !string.IsNullOrWhiteSpace(reason)
            ? $@"
        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Reason:</strong></p>
            <p style=""margin: 10px 0;"">{WebUtility.HtmlEncode(reason)}</p>
        </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Access Request Update</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">Access Request Update</h2>

        <p>Thank you for your interest in MeepleAI.</p>

        <p>Unfortunately, your access request could not be approved at this time.</p>
{reasonSection}
        <p>You may submit a new request in the future.</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            If you have any questions, feel free to contact us.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; {FooterYear} MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }
}
