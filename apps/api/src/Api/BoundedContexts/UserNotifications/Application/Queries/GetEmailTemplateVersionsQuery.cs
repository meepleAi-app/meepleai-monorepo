using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get all versions of a template by name, optionally filtered by locale.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal record GetEmailTemplateVersionsQuery(
    string Name,
    string? Locale = null
) : IQuery<List<EmailTemplateDto>>;
