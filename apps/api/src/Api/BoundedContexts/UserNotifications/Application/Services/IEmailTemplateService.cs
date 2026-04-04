namespace Api.BoundedContexts.UserNotifications.Application.Services;

/// <summary>
/// Service for rendering branded HTML email templates.
/// Issue #4417: Email notification queue with HTML templates.
/// </summary>
internal interface IEmailTemplateService
{
    /// <summary>
    /// Renders email template for document ready notification.
    /// </summary>
    string RenderDocumentReady(string userName, string fileName, string documentUrl);

    /// <summary>
    /// Renders email template for document processing failure.
    /// </summary>
    string RenderDocumentFailed(string userName, string fileName, string errorMessage);

    /// <summary>
    /// Renders email template for retry notification.
    /// </summary>
    string RenderRetryAvailable(string userName, string fileName, int retryCount);

    /// <summary>
    /// Renders email template for admin manual notification.
    /// </summary>
    string RenderAdminNotification(string userName, string title, string message);
}
