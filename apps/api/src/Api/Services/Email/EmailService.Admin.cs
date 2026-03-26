using System.Net;
using System.Net.Mail;
using Api.Infrastructure.Security;

namespace Api.Services;

internal partial class EmailService
{
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
            var body = BuildAdminShareRequestDigestEmailBody(
                toName,
                totalPending,
                oldestPendingDays,
                createdToday,
                pendingByType,
                reviewQueueUrl);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromAddress, _fromName);
            message.To.Add(new MailAddress(toEmail, toName));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = _enableSsl;

            if (!string.IsNullOrEmpty(_smtpUsername) && !string.IsNullOrEmpty(_smtpPassword))
            {
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            }

            await client.SendMailAsync(message, ct).ConfigureAwait(false);

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
            var subject = "New Game Submitted for Approval";
            var body = BuildSharedGameSubmittedEmailBody(toName, gameTitle, submitterName, reviewUrl);

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

    // ===== Template builders =====

    private string BuildAdminShareRequestDigestEmailBody(
        string adminName,
        int totalPending,
        int oldestPendingDays,
        int createdToday,
        Dictionary<string, int> pendingByType,
        string reviewQueueUrl)
    {
        var typeBreakdownRows = string.Join("", pendingByType.Select(kvp =>
            $@"<tr>
                <td style=""padding: 8px; border-bottom: 1px solid #e0e0e0;"">{kvp.Key}</td>
                <td style=""padding: 8px; border-bottom: 1px solid #e0e0e0; text-align: center; font-weight: bold;"">{kvp.Value}</td>
            </tr>"));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Share Requests Daily Digest</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI Admin</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #0c5460; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">📊 Share Requests Daily Digest</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">Review Queue Update</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {adminName},</p>

        <p>Here's your daily summary of share requests waiting for review:</p>

        <div style=""margin: 30px 0;"">
            <div style=""display: grid; grid-template-columns: repeat(3, 1fr); gap: 15px; text-align: center;"">
                <div style=""padding: 20px; background-color: #ffc107; color: white; border-radius: 8px;"">
                    <div style=""font-size: 32px; font-weight: bold; margin-bottom: 5px;"">{totalPending}</div>
                    <div style=""font-size: 14px;"">Total Pending</div>
                </div>
                <div style=""padding: 20px; background-color: #28a745; color: white; border-radius: 8px;"">
                    <div style=""font-size: 32px; font-weight: bold; margin-bottom: 5px;"">{createdToday}</div>
                    <div style=""font-size: 14px;"">New Today</div>
                </div>
                <div style=""padding: 20px; background-color: #dc3545; color: white; border-radius: 8px;"">
                    <div style=""font-size: 32px; font-weight: bold; margin-bottom: 5px;"">{oldestPendingDays}</div>
                    <div style=""font-size: 14px;"">Oldest (days)</div>
                </div>
            </div>
        </div>

        <div style=""margin: 20px 0;"">
            <h3 style=""color: #2c3e50; margin-bottom: 10px;"">Breakdown by Type</h3>
            <table style=""width: 100%; border-collapse: collapse; border: 1px solid #e0e0e0;"">
                <thead>
                    <tr style=""background-color: #f8f9fa;"">
                        <th style=""padding: 10px; text-align: left; border-bottom: 2px solid #e0e0e0;"">Contribution Type</th>
                        <th style=""padding: 10px; text-align: center; border-bottom: 2px solid #e0e0e0;"">Count</th>
                    </tr>
                </thead>
                <tbody>
                    {typeBreakdownRows}
                </tbody>
            </table>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{reviewQueueUrl}"" style=""background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Go to Review Queue</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an automated daily digest. You can manage notification preferences in your admin settings.
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

    private string BuildSharedGameSubmittedEmailBody(
        string adminName,
        string gameTitle,
        string submitterName,
        string reviewUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>New Game Awaiting Approval</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">New Game Awaiting Approval</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{gameTitle}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {adminName},</p>

        <p><strong>{submitterName}</strong> has submitted a new game for approval in the shared catalog.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #28a745; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Game Title:</strong> {gameTitle}</p>
            <p style=""margin: 5px 0;""><strong>Submitted By:</strong> {submitterName}</p>
        </div>

        <p>Please review the submission and approve or request changes as appropriate.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{reviewUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Review Submission</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an admin notification for the approval queue.
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
