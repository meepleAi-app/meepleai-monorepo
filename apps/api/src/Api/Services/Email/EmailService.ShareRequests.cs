using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
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
            var subject = "Share Request Submitted";
            var body = BuildShareRequestCreatedEmailBody(userName, gameTitle, contributionType, shareRequestId);

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
            var subject = "Contribution Approved! 🎉";
            var body = BuildShareRequestApprovedEmailBody(userName, gameTitle, sharedGameId, userId);

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
            var subject = "Share Request Not Approved";
            var body = BuildShareRequestRejectedEmailBody(userName, gameTitle, reason);

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
            var subject = "Changes Requested for Your Contribution";
            var body = BuildShareRequestChangesRequestedEmailBody(userName, gameTitle, feedback, shareRequestId);

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
            var subject = "Your Game Proposal Is Now Under Review";
            var body = BuildShareRequestReviewStartedEmailBody(userName, gameTitle, shareRequestId);

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
            var subject = "Your Game Proposal Has Been Merged";
            var body = BuildShareRequestKbMergedEmailBody(userName, gameTitle, sharedGameId);

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

    // ===== Template builders =====

    private string BuildShareRequestCreatedEmailBody(
        string userName,
        string gameTitle,
        string contributionType,
        Guid shareRequestId)
    {
        var requestUrl = $"{_frontendBaseUrl}/contributions/requests/{shareRequestId}";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Share Request Submitted</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #0c5460; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">📤 Share Request Submitted</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Your request to share <strong>{gameTitle}</strong> has been submitted for review.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Game:</strong> {gameTitle}</p>
            <p style=""margin: 5px 0;""><strong>Type:</strong> {contributionType}</p>
        </div>

        <p>Our review team will evaluate your contribution and get back to you shortly. You can track the status of your request at any time.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{requestUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">View Request Status</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for contributing to the MeepleAI community!
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

    private string BuildShareRequestApprovedEmailBody(
        string userName,
        string gameTitle,
        Guid sharedGameId,
        Guid userId)
    {
        var sharedGameUrl = $"{_frontendBaseUrl}/shared-games/{sharedGameId}";
        var contributorProfileUrl = $"{_frontendBaseUrl}/users/{userId}/contributions";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Contribution Approved</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">🎉 Contribution Approved!</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Great news! Your contribution for <strong>{gameTitle}</strong> has been approved and is now available in the shared catalog.</p>

        <p>Your contribution is now visible to the entire MeepleAI community and will help other players discover and enjoy this game.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{sharedGameUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold; margin-right: 10px;"">View in Catalog</a>
            <a href=""{contributorProfileUrl}"" style=""background-color: #6c757d; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">My Contributions</a>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Did you know?</strong> Your contributions may earn you contributor badges. Keep sharing to unlock new achievements!</p>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for being an awesome contributor to the MeepleAI community!
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

    private string BuildShareRequestRejectedEmailBody(
        string userName,
        string gameTitle,
        string reason)
    {
        var guidelinesUrl = $"{_frontendBaseUrl}/help/contribution-guidelines";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Share Request Not Approved</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">Share Request Not Approved</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Thank you for your interest in contributing to the MeepleAI shared catalog. After review, we're unable to approve your request to share <strong>{gameTitle}</strong> at this time.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Reason:</strong></p>
            <p style=""margin: 10px 0;"">{reason}</p>
        </div>

        <p>We encourage you to review our contribution guidelines and consider submitting again with any necessary adjustments.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{guidelinesUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">View Guidelines</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            We appreciate your understanding and hope to see your contributions in the future.
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

    private string BuildShareRequestChangesRequestedEmailBody(
        string userName,
        string gameTitle,
        string feedback,
        Guid shareRequestId)
    {
        var editUrl = $"{_frontendBaseUrl}/contributions/requests/{shareRequestId}/edit";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Changes Requested</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #0c5460; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">🔄 Changes Requested</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>The reviewer has requested some changes for your <strong>{gameTitle}</strong> submission before it can be approved.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #0c5460; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Reviewer Feedback:</strong></p>
            <p style=""margin: 10px 0;"">{feedback}</p>
        </div>

        <p>Please review the feedback and make the requested changes. Once updated, your submission will be reviewed again.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{editUrl}"" style=""background-color: #0c5460; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Edit Submission</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for your patience. We look forward to seeing your updated submission!
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

    private string BuildShareRequestReviewStartedEmailBody(
        string userName,
        string gameTitle,
        Guid shareRequestId)
    {
        var requestUrl = $"{_frontendBaseUrl}/contributions/requests/{shareRequestId}";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Game Proposal Under Review</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #fff3cd; padding: 20px; border-radius: 5px; border: 2px solid #ffc107; margin-bottom: 20px;"">
        <h2 style=""color: #856404; margin-top: 0;"">🔍 Game Proposal Under Review</h2>
        <p style=""margin: 0; color: #856404; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Good news! An admin has started reviewing your game proposal for <strong>{gameTitle}</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Game:</strong> {gameTitle}</p>
            <p style=""margin: 5px 0;""><strong>Status:</strong> Under Admin Review</p>
        </div>

        <p>Our review team is carefully evaluating your contribution. You'll receive another notification once the review is complete.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{requestUrl}"" style=""background-color: #ffc107; color: #212529; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Track Review Progress</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for your patience!
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

    private string BuildShareRequestKbMergedEmailBody(
        string userName,
        string gameTitle,
        Guid sharedGameId)
    {
        var sharedGameUrl = $"{_frontendBaseUrl}/shared-games/{sharedGameId}";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Game Proposal Merged</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">🎉 Game Proposal Merged!</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Great news! Your game proposal for <strong>{gameTitle}</strong> has been approved and merged into the existing game in our catalog.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #d4edda; border-left: 4px solid #28a745;"">
            <p style=""margin: 0; color: #155724;""><strong>✓ Your knowledge base (PDFs and documents) has been added to the existing game.</strong></p>
        </div>

        <p>Your contribution enhances the existing game's knowledge base and will help the entire MeepleAI community. Since the game already existed in our catalog, no migration action is needed on your part.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{sharedGameUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">View Updated Game</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for enriching the MeepleAI knowledge base!
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
