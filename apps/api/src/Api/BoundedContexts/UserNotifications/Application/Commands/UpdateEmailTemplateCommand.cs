using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to update an existing email template's content.
/// Issue #53: CQRS commands for admin email template management.
/// </summary>
internal record UpdateEmailTemplateCommand(
    Guid Id,
    string Subject,
    string HtmlBody,
    Guid ModifiedBy
) : ICommand<bool>;
