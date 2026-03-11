using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to preview a rendered email template with test placeholder data.
/// Returns the rendered HTML with placeholders replaced by test values.
/// Issue #53: CQRS commands for admin email template management.
/// </summary>
internal record PreviewEmailTemplateCommand(
    Guid Id,
    Dictionary<string, string>? TestData
) : ICommand<string>;
