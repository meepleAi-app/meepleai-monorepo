using Api.BoundedContexts.UserNotifications.Application.Services;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// Renders branded HTML email templates with MeepleAI styling.
/// Supports dark mode, responsive layout, and unsubscribe link.
/// Issue #4417: Email notification queue with HTML templates.
/// </summary>
internal sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly string _frontendBaseUrl;

    public EmailTemplateService(IConfiguration configuration)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
#pragma warning restore S1075
    }

    public string RenderDocumentReady(string userName, string fileName, string documentUrl)
    {
        var content = $@"
        <div style=""background-color: #d4edda; padding: 20px; border-radius: 8px; border: 2px solid #28a745; margin-bottom: 20px;"">
            <h2 style=""color: #155724; margin-top: 0;"">PDF Ready</h2>
            <p style=""margin: 0; color: #155724; font-weight: bold;"">{Escape(fileName)}</p>
        </div>

        <p>Hello {Escape(userName)},</p>

        <p>Great news! Your PDF document <strong>{Escape(fileName)}</strong> has been successfully processed and is now ready for AI queries.</p>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{Escape(documentUrl)}"" style=""background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Start Chatting</a>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>Quick Tips:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ask questions about the content</li>
                <li>Request summaries of sections</li>
                <li>Get clarification on complex topics</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Happy exploring!
        </p>";

        return WrapInBaseTemplate("PDF Ready - MeepleAI", content);
    }

    public string RenderDocumentFailed(string userName, string fileName, string errorMessage)
    {
        var supportUrl = $"{_frontendBaseUrl}/support";

        var content = $@"
        <div style=""background-color: #f8d7da; padding: 20px; border-radius: 8px; border: 2px solid #dc3545; margin-bottom: 20px;"">
            <h2 style=""color: #721c24; margin-top: 0;"">PDF Processing Failed</h2>
            <p style=""margin: 0; color: #721c24; font-weight: bold;"">{Escape(fileName)}</p>
        </div>

        <p>Hello {Escape(userName)},</p>

        <p>Unfortunately, we encountered an issue while processing your PDF document <strong>{Escape(fileName)}</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #dc3545; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>Error Details:</strong></p>
            <p style=""margin: 5px 0 0 0; color: #666;"">{Escape(errorMessage)}</p>
        </div>

        <div style=""margin: 20px 0; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>Common Solutions:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Ensure PDF is not password-protected</li>
                <li>Check file size is under limit</li>
                <li>Verify PDF is not corrupted</li>
            </ul>
        </div>

        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{Escape(supportUrl)}"" style=""background-color: #6c757d; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;"">Contact Support</a>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            If the problem persists, please reach out to our support team.
        </p>";

        return WrapInBaseTemplate("PDF Processing Failed - MeepleAI", content);
    }

    public string RenderRetryAvailable(string userName, string fileName, int retryCount)
    {
        var content = $@"
        <div style=""background-color: #fff3cd; padding: 20px; border-radius: 8px; border: 2px solid #ffc107; margin-bottom: 20px;"">
            <h2 style=""color: #856404; margin-top: 0;"">PDF Processing Retry</h2>
            <p style=""margin: 0; color: #856404; font-weight: bold;"">{Escape(fileName)}</p>
        </div>

        <p>Hello {Escape(userName)},</p>

        <p>We are retrying the processing of your PDF document <strong>{Escape(fileName)}</strong>. This is retry attempt <strong>#{retryCount}</strong>.</p>

        <div style=""margin: 20px 0; padding: 15px; background-color: #f8f9fa; border-radius: 3px;"">
            <p style=""margin: 0;""><strong>What happens next:</strong></p>
            <ul style=""margin: 10px 0 0 0; padding-left: 20px;"">
                <li>Our system will automatically retry processing</li>
                <li>You will be notified when processing completes</li>
                <li>No action is needed on your part</li>
            </ul>
        </div>

        <p style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; font-size: 14px; color: #666;"">
            Thank you for your patience!
        </p>";

        return WrapInBaseTemplate("PDF Retry - MeepleAI", content);
    }

    private string WrapInBaseTemplate(string title, string content)
    {
        var preferencesUrl = $"{_frontendBaseUrl}/settings/notifications";

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta name=""color-scheme"" content=""light dark"">
    <title>{Escape(title)}</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #ffffff;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;"">
        <h1 style=""color: #2c3e50; margin: 0;"">MeepleAI</h1>
    </div>

    <div style=""background-color: #ffffff; padding: 30px; border-radius: 8px; border: 1px solid #e0e0e0;"">
        {content}
    </div>

    <div style=""margin-top: 20px; text-align: center; font-size: 12px; color: #999;"">
        <p>This is an automated message, please do not reply to this email.</p>
        <p><a href=""{Escape(preferencesUrl)}"" style=""color: #999;"">Manage notification preferences</a></p>
        <p>&copy; {DateTime.UtcNow.Year} MeepleAI. All rights reserved.</p>
    </div>
</body>
</html>";
    }

    private static string Escape(string input)
    {
        return System.Net.WebUtility.HtmlEncode(input);
    }
}
