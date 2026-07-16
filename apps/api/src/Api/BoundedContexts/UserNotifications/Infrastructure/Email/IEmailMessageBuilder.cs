using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Email;

/// <summary>
/// Context passed to an <see cref="IEmailMessageBuilder"/> to render a notification into email content.
/// </summary>
/// <param name="Type">The notification type (drives subject/title resolution).</param>
/// <param name="Payload">The polymorphic notification payload carrying per-type data.</param>
/// <param name="DeepLinkPath">Optional relative deep-link path used to build an "Open in MeepleAI" CTA.</param>
/// <param name="RecipientName">Display name (or email) used for the greeting.</param>
internal sealed record EmailBuildContext(
    NotificationType Type,
    INotificationPayload Payload,
    string? DeepLinkPath,
    string RecipientName);

/// <summary>
/// Rendered email content: subject line + branded HTML body, ready for
/// <c>IEmailService.SendRawEmailAsync</c>.
/// </summary>
internal sealed record EmailMessage(string Subject, string HtmlBody);

/// <summary>
/// Renders a notification (type + payload + deep link) into an <see cref="EmailMessage"/>.
/// Mirrors <c>ISlackMessageBuilder</c>: <see cref="EmailMessageBuilderFactory"/> resolves the correct
/// builder per <see cref="NotificationType"/>, falling back to <c>GenericEmailBuilder</c> (issue #3026).
/// </summary>
internal interface IEmailMessageBuilder
{
    /// <summary>
    /// Returns true when this builder produces a dedicated email layout for the given type.
    /// The generic fallback returns false (selected only when no specific builder matches).
    /// </summary>
    bool CanHandle(NotificationType type);

    /// <summary>
    /// Builds the subject + HTML body for the given notification context.
    /// </summary>
    EmailMessage BuildMessage(EmailBuildContext context);
}
