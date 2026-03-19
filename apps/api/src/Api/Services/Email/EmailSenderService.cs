using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services.Email;

/// <summary>
/// Sends emails via SMTP.
/// Delegates HTML body construction to <see cref="IEmailTemplateService"/>.
/// Implements <see cref="IEmailService"/> so all existing consumers require no changes.
/// </summary>
internal sealed class EmailSenderService : IEmailService
{
    private readonly IEmailTemplateService _templates;
    private readonly ILogger<EmailSenderService> _logger;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _smtpUsername;
    private readonly string? _smtpPassword;
    private readonly bool _enableSsl;
    private readonly string _resetUrlBase;
    private readonly string _frontendBaseUrl;

    public EmailSenderService(
        IEmailTemplateService templates,
        IConfiguration configuration,
        ILogger<EmailSenderService> logger)
    {
        _templates = templates;
        _logger = logger;

#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultResetUrlBase = "http://localhost:3000/reset-password";
        const string DefaultFrontendBaseUrl = "http://localhost:3000";
#pragma warning restore S1075

        // Load email configuration with fallback to SMTP_* env var names from email.secret
        // email.secret uses SMTP_HOST/SMTP_USER/SMTP_PASSWORD but EmailService originally
        // expected Email:SmtpHost/Email:SmtpUsername/Email:SmtpPassword — bridge the gap
        _fromAddress = configuration["Email:FromAddress"] ?? configuration["SMTP_FROM_EMAIL"] ?? "noreply@meepleai.dev";
        _fromName = configuration["Email:FromName"] ?? "MeepleAI";
        _smtpHost = configuration["Email:SmtpHost"] ?? configuration["SMTP_HOST"] ?? "localhost";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? configuration["SMTP_PORT"] ?? "587", CultureInfo.InvariantCulture);
        _smtpUsername = configuration["Email:SmtpUsername"] ?? configuration["SMTP_USER"];
        _smtpPassword = configuration["Email:SmtpPassword"] ?? configuration["SMTP_PASSWORD"] ?? configuration["GMAIL_APP_PASSWORD"];
        _enableSsl = bool.Parse(configuration["Email:EnableSsl"] ?? "true");
        _resetUrlBase = configuration["Email:ResetUrlBase"] ?? DefaultResetUrlBase;
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? DefaultFrontendBaseUrl;
    }

    // ── Core dispatch helpers ────────────────────────────────────────────────

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_smtpHost, _smtpPort);
        client.EnableSsl = _enableSsl;

        if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
        {
            client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
        }

        return client;
    }

    private MailMessage CreateMessage(string toEmail, string toName, string subject, string body)
    {
        var message = new MailMessage();
        message.From = new MailAddress(_fromAddress, _fromName);
        message.To.Add(new MailAddress(toEmail, toName));
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = true;
        return message;
    }

    private MailMessage CreateMessageNoName(string toEmail, string subject, string body)
    {
        var message = new MailMessage();
        message.From = new MailAddress(_fromAddress, _fromName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = true;
        return message;
    }

    // ── IEmailService implementation ─────────────────────────────────────────

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken,
        CancellationToken ct = default)
    {
        try
        {
            var resetUrl = $"{_resetUrlBase}?token={Uri.EscapeDataString(resetToken)}";
            var body = _templates.BuildPasswordResetEmailBody(toName, resetUrl);

            using var message = CreateMessage(toEmail, toName, "Reset Your MeepleAI Password", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildTwoFactorDisabledEmailBody(toName, wasAdminOverride);

            using var message = CreateMessage(toEmail, toName, "Two-Factor Authentication Disabled", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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
        CancellationToken ct = default)
    {
        try
        {
            var verifyUrl = $"{_frontendBaseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
            var body = _templates.BuildVerificationEmailBody(toName, verifyUrl);

            using var message = CreateMessage(toEmail, toName, "Verify Your MeepleAI Email", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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

    // ISSUE-918: Report email delivery
    public async Task SendReportEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string reportDescription,
        byte[] reportContent,
        string fileName,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        if (recipients is null || recipients.Count == 0)
        {
            _logger.LogWarning("No recipients provided for report email: {ReportName}", reportName);
            return;
        }

        const long MaxAttachmentSize = 10 * 1024 * 1024; // 10 MB
        if (fileSizeBytes > MaxAttachmentSize)
        {
            _logger.LogWarning(
                "Report too large for email attachment: {FileName} ({Size} bytes). Limit: {Limit} bytes",
                fileName, fileSizeBytes, MaxAttachmentSize);
            throw new InvalidOperationException($"Report size ({fileSizeBytes} bytes) exceeds email attachment limit ({MaxAttachmentSize} bytes)");
        }

        try
        {
            var subject = $"[MeepleAI] Report Ready: {reportName}";
            var body = _templates.BuildReportEmailBody(reportName, reportDescription, fileName, fileSizeBytes);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            var attachment = new System.Net.Mail.Attachment(
                new System.IO.MemoryStream(reportContent),
                fileName);
            message.Attachments.Add(attachment);

            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Report email sent successfully: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: Wraps external SMTP service exceptions (authentication, network, timeout) into domain exception
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send report email: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
            throw new InvalidOperationException($"Failed to send report email for {reportName}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendReportFailureEmailAsync(
        IReadOnlyList<string> recipients,
        string reportName,
        string errorMessage,
        CancellationToken ct = default)
    {
        if (recipients is null || recipients.Count == 0)
        {
            _logger.LogWarning("No recipients provided for report failure email: {ReportName}", reportName);
            return;
        }

        try
        {
            var subject = $"[MeepleAI] Report Generation Failed: {reportName}";
            var body = _templates.BuildReportFailureEmailBody(reportName, errorMessage);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Report failure email sent successfully: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: Failure notification email is best-effort, must not propagate exceptions
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send report failure email: {ReportName} to {RecipientCount} recipients",
                reportName, recipients.Count);
        }
#pragma warning restore CA1031
    }

    // ===== ISSUE-2739: Share Request Notification Emails =====

    public async Task SendShareRequestCreatedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string contributionType,
        Guid shareRequestId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestCreatedEmailBody(userName, gameTitle, contributionType, shareRequestId);

            using var message = CreateMessage(toEmail, userName, "Share Request Submitted", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Share request created email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send share request created email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send share request created email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendShareRequestApprovedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid sharedGameId,
        Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestApprovedEmailBody(userName, gameTitle, sharedGameId, userId);

            using var message = CreateMessage(toEmail, userName, "Contribution Approved! 🎉", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Share request approved email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send share request approved email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send share request approved email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendShareRequestRejectedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string reason,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestRejectedEmailBody(userName, gameTitle, reason);

            using var message = CreateMessage(toEmail, userName, "Share Request Not Approved", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Share request rejected email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send share request rejected email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send share request rejected email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendShareRequestChangesRequestedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        string feedback,
        Guid shareRequestId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestChangesRequestedEmailBody(userName, gameTitle, feedback, shareRequestId);

            using var message = CreateMessage(toEmail, userName, "Changes Requested for Your Contribution", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Share request changes requested email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send share request changes requested email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send share request changes requested email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-2740: Admin share request digest email
    public async Task SendAdminShareRequestDigestEmailAsync(
        string toEmail,
        string toName,
        int totalPending,
        int oldestPendingDays,
        int createdToday,
        Dictionary<string, int> pendingByType,
        string reviewQueueUrl,
        CancellationToken ct = default)
    {
        try
        {
            var subject = $"[MeepleAI Admin] Share Request Digest - {totalPending} Pending";
            var body = _templates.BuildAdminShareRequestDigestEmailBody(
                toName,
                totalPending,
                oldestPendingDays,
                createdToday,
                pendingByType,
                reviewQueueUrl);

            using var message = CreateMessage(toEmail, toName, subject, body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Admin digest email sent: To={MaskedEmail}, TotalPending={TotalPending}",
                DataMasking.MaskEmail(toEmail),
                totalPending);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send admin digest email to {MaskedEmail}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send admin digest email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-2741: Badge earned email implementation
    public async Task SendBadgeEarnedEmailAsync(
        string toEmail,
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string badgeTierColor,
        string profileUrl,
        string shareText,
        CancellationToken ct = default)
    {
        try
        {
            var subject = $"🎉 Badge Earned: {badgeName}!";
            var body = _templates.BuildBadgeEarnedEmailBody(
                userName,
                badgeName,
                badgeDescription,
                badgeIconUrl,
                badgeTier,
                badgeTierColor,
                profileUrl,
                shareText);

            using var message = CreateMessage(toEmail, userName, subject, body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Badge earned email sent successfully to {Email} for badge {BadgeName}",
                DataMasking.MaskEmail(toEmail),
                badgeName);
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
                "Failed to send badge earned email to {Email} for badge {BadgeName}",
                DataMasking.MaskEmail(toEmail),
                badgeName);
            throw new InvalidOperationException($"Failed to send badge earned email for {badgeName}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendMilestoneBadgeEarnedEmailAsync(
        string toEmail,
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string milestoneMessage,
        int totalContributions,
        string profileUrl,
        string leaderboardUrl,
        CancellationToken ct = default)
    {
        try
        {
            var subject = $"🌟 Milestone Achievement: {badgeName}!";
            var body = _templates.BuildMilestoneBadgeEarnedEmailBody(
                userName,
                badgeName,
                badgeDescription,
                badgeIconUrl,
                badgeTier,
                milestoneMessage,
                totalContributions,
                profileUrl,
                leaderboardUrl);

            using var message = CreateMessage(toEmail, userName, subject, body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Milestone badge email sent successfully to {Email} for badge {BadgeName}",
                DataMasking.MaskEmail(toEmail),
                badgeName);
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
                "Failed to send milestone badge email to {Email} for badge {BadgeName}",
                DataMasking.MaskEmail(toEmail),
                badgeName);
            throw new InvalidOperationException($"Failed to send milestone badge email for {badgeName}", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-2742: Rate limit cooldown ended email implementation
    public async Task SendCooldownEndedEmailAsync(
        string toEmail,
        string userName,
        int remainingMonthly,
        int remainingPending,
        string libraryUrl,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildCooldownEndedEmailBody(userName, remainingMonthly, remainingPending, libraryUrl);

            using var message = CreateMessage(toEmail, userName, "Ready to Contribute Again! 🎉", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Cooldown ended email sent successfully to {Email}",
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
                "Failed to send cooldown ended email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send cooldown ended email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-2886: User suspension notification emails
    public async Task SendAccountSuspendedEmailAsync(
        string toEmail,
        string userName,
        string? reason,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildAccountSuspendedEmailBody(userName, reason);

            using var message = CreateMessage(toEmail, userName, "Your MeepleAI Account Has Been Suspended", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildAccountReactivatedEmailBody(userName);

            using var message = CreateMessage(toEmail, userName, "Your MeepleAI Account Has Been Reactivated", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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
            var body = _templates.BuildAccountLockedEmailBody(userName, failedAttempts, lockedUntil, ipAddress);

            using var message = CreateMessage(toEmail, userName, "Security Alert: Your MeepleAI Account Has Been Locked", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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

    // ISSUE-3668: Game proposal lifecycle notification emails
    public async Task SendShareRequestReviewStartedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid shareRequestId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestReviewStartedEmailBody(userName, gameTitle, shareRequestId);

            using var message = CreateMessage(toEmail, userName, "Your Game Proposal Is Now Under Review", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Review started email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send review started email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send review started email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendShareRequestKbMergedEmailAsync(
        string toEmail,
        string userName,
        string gameTitle,
        Guid sharedGameId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildShareRequestKbMergedEmailBody(userName, gameTitle, sharedGameId);

            using var message = CreateMessage(toEmail, userName, "Your Game Proposal Has Been Merged", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "KB merged email sent to {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send KB merged email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send KB merged email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-4159: Shared game submission notification for admins
    public async Task SendSharedGameSubmittedForApprovalEmailAsync(
        string toEmail,
        string toName,
        string gameTitle,
        string submitterName,
        Guid gameId,
        CancellationToken ct = default)
    {
        try
        {
            var reviewUrl = $"{_frontendBaseUrl}/admin/approval-queue?gameId={gameId}";
            var body = _templates.BuildSharedGameSubmittedEmailBody(toName, gameTitle, submitterName, reviewUrl);

            using var message = CreateMessage(toEmail, toName, "New Game Submitted for Approval", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Shared game approval notification email sent to admin {Email} for game {GameTitle}",
                DataMasking.MaskEmail(toEmail),
                gameTitle);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send shared game approval notification email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send shared game approval notification email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-4220: PDF notification emails
    public async Task SendPdfReadyEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        Guid pdfDocumentId,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildPdfReadyEmailBody(userName, fileName, pdfDocumentId);

            using var message = CreateMessage(toEmail, userName, "Your PDF is Ready - MeepleAI", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF ready email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF ready email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF ready email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendPdfFailedEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        string errorMessage,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildPdfFailedEmailBody(userName, fileName, errorMessage);

            using var message = CreateMessage(toEmail, userName, "PDF Processing Failed - MeepleAI", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF failed email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF failed email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF failed email", ex);
        }
#pragma warning restore CA1031
    }

    public async Task SendPdfRetryEmailAsync(
        string toEmail,
        string userName,
        string fileName,
        int retryCount,
        CancellationToken ct = default)
    {
        try
        {
            var body = _templates.BuildPdfRetryEmailBody(userName, fileName, retryCount);

            using var message = CreateMessage(toEmail, userName, "PDF Processing Retry - MeepleAI", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF retry email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PDF retry email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send PDF retry email", ex);
        }
#pragma warning restore CA1031
    }

    // ISSUE-124: Invitation system emails
    public async Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string token,
        string invitedByName,
        CancellationToken ct = default)
    {
        try
        {
            var inviteLink = $"{_frontendBaseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";
            var body = _templates.BuildInvitationEmailBody(role, inviteLink, invitedByName);

            using var message = CreateMessageNoName(toEmail, "You've been invited to MeepleAI", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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
            var setupLink = $"{_frontendBaseUrl}/setup-account?token={Uri.EscapeDataString(token)}";
            var body = _templates.BuildEnhancedInvitationEmailBody(displayName, role, setupLink, invitedByName, customMessage, expiresAt);

            using var message = CreateMessageNoName(toEmail, "Sei stato invitato su MeepleAI!", body);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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

    // ISSUE-4417: Raw email sending for queue processor
    /// <summary>
    /// Sends a pre-rendered HTML email via SMTP.
    /// Used by EmailProcessorJob for queue-based delivery.
    /// </summary>
    public async Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            using var message = CreateMessageNoName(toEmail, subject, htmlBody);
            using var smtpClient = CreateSmtpClient();
            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Raw email sent successfully to {Email}",
                DataMasking.MaskEmail(toEmail));
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Failed to send raw email to {Email}",
                DataMasking.MaskEmail(toEmail));
            throw new InvalidOperationException("Failed to send raw email", ex);
        }
    }
}
