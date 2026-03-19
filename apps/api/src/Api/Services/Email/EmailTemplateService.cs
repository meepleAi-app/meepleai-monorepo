using System.Globalization;

namespace Api.Services.Email;

/// <summary>
/// Renders HTML email bodies for all notification types.
/// Uses URL configuration to build links embedded in templates.
/// </summary>
internal sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly string _frontendBaseUrl;

    public EmailTemplateService(IConfiguration configuration)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultFrontendBaseUrl = "http://localhost:3000";
#pragma warning restore S1075

        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? DefaultFrontendBaseUrl;
    }

    public string BuildVerificationEmailBody(string userName, string verifyUrl)
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

    public string BuildPasswordResetEmailBody(string userName, string resetUrl)
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

    public string BuildTwoFactorDisabledEmailBody(string userName, bool wasAdminOverride)
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

    public string BuildReportEmailBody(
        string reportName,
        string reportDescription,
        string fileName,
        long fileSizeBytes)
    {
        var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Report Ready</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #0c5460; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">📊 Report Ready</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{reportName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Your scheduled report has been generated successfully.</p>

        <p><strong>Description:</strong> {reportDescription}</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>File:</strong> {fileName}</p>
            <p style=""margin: 5px 0;""><strong>Size:</strong> {fileSizeMB:F2} MB</p>
            <p style=""margin: 5px 0;""><strong>Generated:</strong> {generatedAt} UTC</p>
        </div>

        <p>The report is attached to this email. Please review the contents at your convenience.</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an automated report delivery from your scheduled reports.
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

    public string BuildReportFailureEmailBody(string reportName, string errorMessage)
    {
        var failedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Report Generation Failed</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">⚠️ Report Generation Failed</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">{reportName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>An error occurred while generating your scheduled report.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 5px 0;""><strong>Failed At:</strong> {failedAt} UTC</p>
            <p style=""margin: 5px 0;""><strong>Error:</strong></p>
            <p style=""margin: 10px 0; padding: 10px; background-color: #fff; border: 1px solid #e0e0e0; font-family: monospace; font-size: 12px; word-break: break-word;"">{errorMessage}</p>
        </div>

        <p>The report generation will be retried on the next scheduled run. If this problem persists, please contact the administrator.</p>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            This is an automated notification from your scheduled reports monitoring system.
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

    public string BuildShareRequestCreatedEmailBody(
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

    public string BuildShareRequestApprovedEmailBody(
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

    public string BuildShareRequestRejectedEmailBody(
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

    public string BuildShareRequestChangesRequestedEmailBody(
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

    public string BuildAdminShareRequestDigestEmailBody(
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

    public string BuildBadgeEarnedEmailBody(
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

    public string BuildMilestoneBadgeEarnedEmailBody(
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

    public string BuildCooldownEndedEmailBody(
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

    public string BuildAccountSuspendedEmailBody(string userName, string? reason)
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

    public string BuildAccountReactivatedEmailBody(string userName)
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

    public string BuildAccountLockedEmailBody(
        string userName,
        int failedAttempts,
        DateTime lockedUntil,
        string? ipAddress)
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

    public string BuildShareRequestReviewStartedEmailBody(
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

    public string BuildShareRequestKbMergedEmailBody(
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

    public string BuildSharedGameSubmittedEmailBody(
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

    public string BuildPdfReadyEmailBody(
        string userName,
        string fileName,
        Guid pdfDocumentId)
    {
        var documentUrl = $"{_frontendBaseUrl}/documents/{pdfDocumentId}";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Ready</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d4edda; padding: 20px; border-radius: 5px; border: 2px solid #28a745; margin-bottom: 20px;"">
        <h2 style=""color: #155724; margin-top: 0;"">✅ PDF Ready</h2>
        <p style=""margin: 0; color: #155724; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Great news! Your PDF document <strong>{fileName}</strong> has been successfully processed and is now ready for AI queries.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{documentUrl}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Start Chatting</a>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Quick Tips:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ask questions about the content</li>
                <li>Request summaries of sections</li>
                <li>Get clarification on complex topics</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Happy exploring!
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

    public string BuildPdfFailedEmailBody(
        string userName,
        string fileName,
        string errorMessage)
    {
        var supportUrl = $"{_frontendBaseUrl}/support";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Processing Failed</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #f8d7da; padding: 20px; border-radius: 5px; border: 2px solid #dc3545; margin-bottom: 20px;"">
        <h2 style=""color: #721c24; margin-top: 0;"">⚠️ PDF Processing Failed</h2>
        <p style=""margin: 0; color: #721c24; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>Unfortunately, we encountered an issue while processing your PDF document <strong>{fileName}</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>Error Details:</strong></p>
            <p style=""margin: 5px 0 0 0; color: #666;"">{errorMessage}</p>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>💡 Common Solutions:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ensure PDF is not password-protected</li>
                <li>Check file size is under limit</li>
                <li>Verify PDF is not corrupted</li>
            </ul>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{supportUrl}"" style=""background-color: #6c757d; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Contact Support</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            If the problem persists, please reach out to our support team.
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

    public string BuildPdfRetryEmailBody(
        string userName,
        string fileName,
        int retryCount)
    {
        var libraryUrl = $"{_frontendBaseUrl}/library";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PDF Processing Retry</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #d1ecf1; padding: 20px; border-radius: 5px; border: 2px solid #17a2b8; margin-bottom: 20px;"">
        <h2 style=""color: #0c5460; margin-top: 0;"">🔄 PDF Retry #{retryCount}</h2>
        <p style=""margin: 0; color: #0c5460; font-weight: bold;"">{fileName}</p>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 5px; border: 1px solid #e0e0e0;"">
        <p>Hello {userName},</p>

        <p>We're retrying the processing of your PDF document <strong>{fileName}</strong>. This is attempt #{retryCount}.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>What's Happening:</strong></p>
            <p style=""margin: 5px 0 0 0; color: #666;"">Our system automatically retries failed documents to ensure successful processing. You'll be notified once processing completes.</p>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{libraryUrl}"" style=""background-color: #17a2b8; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">View My Library</a>
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

    public string BuildInvitationEmailBody(string role, string inviteLink, string invitedByName)
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

    public string BuildEnhancedInvitationEmailBody(
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
}
