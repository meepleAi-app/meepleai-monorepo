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

    /// <summary>
    /// Renders a generic branded notification email: title heading + greeting + body paragraph,
    /// with an optional "Open in MeepleAI" deep-link CTA button. Used by GenericEmailBuilder (issue #3026)
    /// to render notification-queue email items that have no dedicated per-type template.
    /// </summary>
    /// <param name="userName">Recipient display name (or email) for the greeting.</param>
    /// <param name="title">Friendly notification title, used as the heading and subject.</param>
    /// <param name="bodyText">Human-readable body paragraph.</param>
    /// <param name="deepLinkPath">Optional relative path; when present a CTA button links to it.</param>
    string RenderGenericNotification(string userName, string title, string bodyText, string? deepLinkPath);
}
