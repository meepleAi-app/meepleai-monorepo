using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to list email templates with optional type and locale filtering.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal record GetEmailTemplatesQuery(
    string? Type = null,
    string? Locale = null
) : IQuery<List<EmailTemplateDto>>;
