using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to publish (activate) a specific email template version.
/// Deactivates all other versions of the same (name, locale) pair.
/// Issue #53: CQRS commands for admin email template management.
/// </summary>
internal record PublishEmailTemplateCommand(Guid Id) : ICommand<bool>;
