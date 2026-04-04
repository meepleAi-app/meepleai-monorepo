using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get a single email template by ID.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal record GetEmailTemplateByIdQuery(Guid Id) : IQuery<EmailTemplateDto?>;
