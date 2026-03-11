namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// Data transfer object for email template.
/// Issue #52: P4.1 Admin email template management.
/// </summary>
public record EmailTemplateDto(
    Guid Id,
    string Name,
    string Locale,
    string Subject,
    string HtmlBody,
    int Version,
    bool IsActive,
    Guid? LastModifiedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
