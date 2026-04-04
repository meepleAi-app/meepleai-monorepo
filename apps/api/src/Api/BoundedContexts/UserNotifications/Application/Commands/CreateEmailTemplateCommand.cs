using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to create a new email template.
/// Issue #53: CQRS commands for admin email template management.
/// </summary>
internal record CreateEmailTemplateCommand(
    string Name,
    string Locale,
    string Subject,
    string HtmlBody,
    Guid CreatedBy
) : ICommand<Guid>;
