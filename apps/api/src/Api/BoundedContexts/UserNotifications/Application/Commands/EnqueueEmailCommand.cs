using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to enqueue an email for async delivery via the email queue.
/// Issue #4417: Email notification queue.
/// </summary>
internal record EnqueueEmailCommand(
    Guid UserId,
    string To,
    string Subject,
    string TemplateName,
    string UserName,
    string FileName,
    string? DocumentUrl = null,
    string? ErrorMessage = null,
    int? RetryCount = null,
    Guid? CorrelationId = null
) : ICommand<Guid>;
