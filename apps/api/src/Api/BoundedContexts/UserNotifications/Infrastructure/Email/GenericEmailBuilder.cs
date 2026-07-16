using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Email;

/// <summary>
/// Fallback email builder for notification types without a dedicated per-type builder (MVP, issue #3026).
/// Subject = friendly title from <see cref="NotificationTitleResolver"/>; body = <c>GenericPayload.Body</c>
/// when present, else the title, wrapped in the branded shell with an optional deep-link CTA button.
/// Always returns false from <see cref="CanHandle"/> — used only as the factory fallback, mirroring
/// <c>GenericSlackBuilder</c>.
/// </summary>
internal sealed class GenericEmailBuilder : IEmailMessageBuilder
{
    private readonly IEmailTemplateService _templateService;

    public GenericEmailBuilder(IEmailTemplateService templateService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
    }

    /// <summary>
    /// Always false: the factory selects this builder only as the fallback, never via CanHandle.
    /// </summary>
    public bool CanHandle(NotificationType type) => false;

    public EmailMessage BuildMessage(EmailBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = NotificationTitleResolver.ResolveTitle(context.Type);

        // Prefer the human-readable body carried by GenericPayload; typed payloads (share request,
        // game night, badge, …) have no generic prose, so fall back to the friendly title. A future
        // per-type IEmailMessageBuilder can render those richly (mirrors the per-type Slack builders).
        var bodyText = context.Payload is GenericPayload generic && !string.IsNullOrWhiteSpace(generic.Body)
            ? generic.Body
            : subject;

        var htmlBody = _templateService.RenderGenericNotification(
            context.RecipientName, subject, bodyText, context.DeepLinkPath);

        return new EmailMessage(subject, htmlBody);
    }
}
