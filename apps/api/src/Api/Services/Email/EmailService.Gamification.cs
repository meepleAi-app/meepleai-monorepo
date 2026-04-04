using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
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
            var body = BuildBadgeEarnedEmailBody(
                userName,
                badgeName,
                badgeDescription,
                badgeIconUrl,
                badgeTier,
                badgeTierColor,
                profileUrl,
                shareText);

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
            var body = BuildMilestoneBadgeEarnedEmailBody(
                userName,
                badgeName,
                badgeDescription,
                badgeIconUrl,
                badgeTier,
                milestoneMessage,
                totalContributions,
                profileUrl,
                leaderboardUrl);

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
            var subject = "Ready to Contribute Again! 🎉";
            var body = BuildCooldownEndedEmailBody(
                userName,
                remainingMonthly,
                remainingPending,
                libraryUrl);

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

    // ===== Template builders =====

    private static string BuildBadgeEarnedEmailBody(
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string badgeTierColor,
        string profileUrl,
        string shareText)
    {
        var badgeIconHtml = !string.IsNullOrEmpty(badgeIconUrl)
            ? $@"<div style=""text-align: center; margin: 20px 0;"">
                    <img src=""{badgeIconUrl}"" alt=""{badgeName}"" style=""width: 120px; height: 120px; border-radius: 50%; border: 4px solid {badgeTierColor};"">
                 </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Badge Earned: {badgeName}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid {badgeTierColor}; margin-bottom: 20px; text-align: center;"">
        <h2 style=""color: #155724; margin-top: 0;"">🎉 Congratulations!</h2>
        <p style=""margin: 0; color: #155724; font-size: 18px; font-weight: bold;"">You've earned a new badge!</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>We're excited to celebrate your achievement with you!</p>

        {badgeIconHtml}

        <div style=""text-align: center; margin: 20px 0;"">
            <h3 style=""color: {badgeTierColor}; margin: 10px 0;"">{badgeName}</h3>
            <p style=""background-color: {badgeTierColor}; color: white; padding: 5px 15px; display: inline-block; border-radius: 15px; font-size: 12px; font-weight: bold;"">{badgeTier} TIER</p>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0; font-style: italic; text-align: center;"">{badgeDescription}</p>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{profileUrl}"" style=""display: inline-block; padding: 12px 30px; background-color: {badgeTierColor}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;"">View Your Badges</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666; text-align: center;"">
            Share your achievement:<br>
            <span style=""font-style: italic; color: #888;"">""{shareText}""</span>
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>Keep up the great work! We look forward to celebrating more achievements with you.</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildMilestoneBadgeEarnedEmailBody(
        string userName,
        string badgeName,
        string badgeDescription,
        string? badgeIconUrl,
        string badgeTier,
        string milestoneMessage,
        int totalContributions,
        string profileUrl,
        string leaderboardUrl)
    {
        var badgeIconHtml = !string.IsNullOrEmpty(badgeIconUrl)
            ? $@"<div style=""text-align: center; margin: 20px 0;"">
                    <img src=""{badgeIconUrl}"" alt=""{badgeName}"" style=""width: 150px; height: 150px; border-radius: 50%; border: 5px solid #FFD700; box-shadow: 0 4px 8px rgba(0,0,0,0.2);"">
                 </div>"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Milestone Achievement: {badgeName}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 5px; margin-bottom: 20px; text-align: center; color: white;"">
        <h2 style=""margin-top: 0; font-size: 28px;"">🌟 MILESTONE ACHIEVEMENT! 🌟</h2>
        <p style=""margin: 0; font-size: 20px; font-weight: bold;"">{badgeName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Dear {userName},</p>

        <p style=""font-size: 18px; font-weight: bold; color: #667eea; text-align: center; margin: 20px 0;"">{milestoneMessage}</p>

        {badgeIconHtml}

        <div style=""text-align: center; margin: 20px 0;"">
            <h3 style=""color: #667eea; margin: 10px 0;"">{badgeName}</h3>
            <p style=""background-color: #FFD700; color: #333; padding: 5px 15px; display: inline-block; border-radius: 15px; font-size: 12px; font-weight: bold;"">{badgeTier} TIER</p>
        </div>

        <div style=""margin: 20px 0; padding: 20px; background-color: #f8f9fa; border-radius: 3px; border-left: 4px solid #667eea;"">
            <p style=""margin: 0; font-style: italic;"">{badgeDescription}</p>
        </div>

        <div style=""text-align: center; margin: 30px 0; padding: 20px; background-color: #fff3cd; border-radius: 5px;"">
            <p style=""margin: 0; font-size: 16px; color: #856404;"">
                <strong>Your Total Contributions:</strong><br>
                <span style=""font-size: 36px; font-weight: bold; color: #667eea;"">{totalContributions}</span>
            </p>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{profileUrl}"" style=""display: inline-block; padding: 12px 30px; background-color: #667eea; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 0 10px;"">View Your Profile</a>
            <a href=""{leaderboardUrl}"" style=""display: inline-block; padding: 12px 30px; background-color: #764ba2; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 0 10px;"">View Leaderboard</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666; text-align: center;"">
            Thank you for being an exceptional member of the MeepleAI community!
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>You're making MeepleAI better for everyone. Thank you!</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }

    private static string BuildCooldownEndedEmailBody(
        string userName,
        int remainingMonthly,
        int remainingPending,
        string libraryUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Ready to Contribute Again</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px; text-align: center;"">
        <h2 style=""color: #155724; margin-top: 0;"">🎉 Ready to Contribute Again!</h2>
        <p style=""margin: 0; color: #155724; font-size: 18px; font-weight: bold;"">Your cooldown period has ended</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hi {userName},</p>

        <p>Great news! Your cooldown period has ended and you can now submit new share requests to the community.</p>

        <div style=""margin: 20px 0; padding: 20px; background-color: #f8f9fa; border-radius: 3px; text-align: center;"">
            <p style=""margin: 0; font-size: 16px; color: #333;""><strong>Your Current Limits:</strong></p>
            <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-top: 15px;"">
                <div style=""padding: 15px; background-color: #28a745; color: white; border-radius: 5px;"">
                    <div style=""font-size: 28px; font-weight: bold;"">{remainingMonthly}</div>
                    <div style=""font-size: 12px;"">Monthly requests remaining</div>
                </div>
                <div style=""padding: 15px; background-color: #17a2b8; color: white; border-radius: 5px;"">
                    <div style=""font-size: 28px; font-weight: bold;"">{remainingPending}</div>
                    <div style=""font-size: 12px;"">Pending slots available</div>
                </div>
            </div>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{libraryUrl}"" style=""display: inline-block; padding: 12px 30px; background-color: #28a745; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;"">Share a Game from Your Library</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666; text-align: center;"">
            Thank you for being part of the MeepleAI community. We look forward to your next contribution!
        </p>
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>Happy sharing!</p>
        <p>&copy; 2025 MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>
";
    }
}
