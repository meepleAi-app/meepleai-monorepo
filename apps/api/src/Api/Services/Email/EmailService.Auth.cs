using System.Globalization;
using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetToken,
        CancellationToken ct = default)
    {
        try
        {
            var resetUrl = $"{_resetUrlBase}?token={Uri.EscapeDataString(resetToken)}";
            var subject = "Reset Your MeepleAI Password";
            var body = BuildPasswordResetEmailBody(toName, resetUrl);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "Two-Factor Authentication Disabled";
            var body = BuildTwoFactorDisabledEmailBody(toName, wasAdminOverride);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "Verify Your MeepleAI Email";
            var body = BuildVerificationEmailBody(toName, verifyUrl);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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

    // ISSUE-2886: User suspension notification emails
    public async Task SendAccountSuspendedEmailAsync(
        string toEmail,
        string userName,
        string? reason,
        CancellationToken ct = default)
    {
        try
        {
            var subject = "Your MeepleAI Account Has Been Suspended";
            var body = BuildAccountSuspendedEmailBody(userName, reason);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "Your MeepleAI Account Has Been Reactivated";
            var body = BuildAccountReactivatedEmailBody(userName);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "Security Alert: Your MeepleAI Account Has Been Locked";
            var body = BuildAccountLockedEmailBody(userName, failedAttempts, lockedUntil, ipAddress);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, userName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "You've been invited to MeepleAI";
            var body = BuildInvitationEmailBody(role, inviteLink, invitedByName);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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
            var subject = "Sei stato invitato su MeepleAI!";
            var body = BuildEnhancedInvitationEmailBody(displayName, role, setupLink, invitedByName, customMessage, expiresAt);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

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

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await smtpClient.SendMailAsync(message, ct).ConfigureAwait(false);

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

    private static string BuildVerificationEmailBody(string userName, string verifyUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Verify Your Email</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">Welcome to MeepleAI!</h2>
        <p style=""margin: 0; color: #155724;"">Please verify your email address to get started.</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Thank you for registering with MeepleAI! To complete your registration and access all features, please verify your email address by clicking the button below:</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{verifyUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Verify Email Address</a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #3498db;"">{verifyUrl}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This verification link will expire in 24 hours for security reasons.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you didn't create a MeepleAI account, you can safely ignore this email.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildPasswordResetEmailBody(string userName, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Reset Your Password</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <h2 style=""color: #2c3e50; margin-top: 0;"">Password Reset Request</h2>

        <p>Hello {userName},</p>

        <p>We received a request to reset your password for your MeepleAI account. If you didn't make this request, you can safely ignore this email.</p>

        <p>To reset your password, click the button below:</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{resetUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Reset Password</a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all; color: #3498db;"">{resetUrl}</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This password reset link will expire in 30 minutes for security reasons.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you continue to have problems, please contact our support team.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildTwoFactorDisabledEmailBody(string userName, bool wasAdminOverride)
    {
        var reason = wasAdminOverride
            ? "An administrator has disabled two-factor authentication on your account, likely due to a support request for lost authenticator access."
            : "Two-factor authentication has been disabled on your account.";

        var warning = wasAdminOverride
            ? "If you did not request this change, please contact our support team immediately and consider re-enabling two-factor authentication."
            : "If you did not make this change, please contact our support team immediately and secure your account.";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Two-Factor Authentication Disabled</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">Security Alert</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">Two-Factor Authentication Disabled</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>{reason}</p>

        <p style=""margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <strong>Important:</strong> {warning}
        </p>

        <p style=""margin-top: 30px;"">To re-enable two-factor authentication:</p>
        <ol>
            <li>Log in to your MeepleAI account</li>
            <li>Go to Settings &gt; Privacy &gt; Two-Factor Authentication</li>
            <li>Follow the setup instructions</li>
        </ol>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This notification was sent for security purposes to keep you informed of changes to your account.
        </p>

        <p style=""font-size: 14px; color: #666;"">
            If you have any questions or concerns, please contact our support team.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated security notification, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountSuspendedEmailBody(string userName, string? reason)
    {
        var reasonSection = !string.IsNullOrWhiteSpace(reason)
            ? $@"
        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Reason for suspension:</strong></p>
            <p style=""margin: 10px 0;"">{reason}</p>
        </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Account Suspended</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">🚨 Account Suspended</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">Your account access has been temporarily suspended</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Your MeepleAI account has been suspended by an administrator. You will not be able to log in until your account is reactivated.</p>
{reasonSection}
        <p>If you believe this suspension was made in error or would like to appeal, please contact our support team.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""mailto:support@meepleai.dev"" style=""background-color: #dc3545; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Contact Support</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            We take account security and community standards seriously. Thank you for your understanding.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountReactivatedEmailBody(string userName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Account Reactivated</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">✅ Account Reactivated</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">Welcome back! Your account is now active</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Good news! Your MeepleAI account has been reactivated by an administrator. You can now log in and access all features again.</p>

        <p>We're glad to have you back in the MeepleAI community!</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #d1ecf1; border-left: 4px solid #17a2b8; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Next Steps:</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>Log in to your account</li>
                <li>Review our community guidelines</li>
                <li>Continue discovering and enjoying board games!</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for being part of the MeepleAI community!
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccountLockedEmailBody(string userName, int failedAttempts, DateTime lockedUntil, string? ipAddress)
    {
        var lockDurationMinutes = (int)Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalMinutes);
        var ipSection = !string.IsNullOrWhiteSpace(ipAddress)
            ? $"<li>IP Address: {ipAddress}</li>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Account Locked</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">🔒 Account Temporarily Locked</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">Too many failed login attempts detected</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Your MeepleAI account has been temporarily locked due to <strong>{failedAttempts} consecutive failed login attempts</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #6c757d; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Details:</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>Failed attempts: {failedAttempts}</li>
                <li>Account locked for: {lockDurationMinutes} minutes</li>
                <li>Unlocks at: {lockedUntil:yyyy-MM-dd HH:mm} UTC</li>
                {ipSection}
            </ul>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #d1ecf1; border-left: 4px solid #17a2b8; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 What to do:</strong></p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>Wait for the lockout period to expire, then try again</li>
                <li>Make sure you're using the correct password</li>
                <li>If you've forgotten your password, use the ""Forgot Password"" link</li>
            </ul>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8d7da; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>⚠️ Security Notice:</strong></p>
            <p style=""margin: 10px 0;"">If you did not attempt these logins, someone may be trying to access your account. We recommend:</p>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>Change your password immediately after regaining access</li>
                <li>Enable two-factor authentication if available</li>
                <li>Review your recent account activity</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            If you need immediate assistance, please contact our support team.
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated security notification, please do not reply to this email.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildInvitationEmailBody(string role, string inviteLink, string invitedByName)
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
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildEnhancedInvitationEmailBody(
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
        <p>&copy; 2025 MeepleAI. Tutti i diritti riservati.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildAccessRequestRejectedEmailBody(string? reason)
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
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }
}
