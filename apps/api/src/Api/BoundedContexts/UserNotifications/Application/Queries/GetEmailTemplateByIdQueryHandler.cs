using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetEmailTemplateByIdQuery.
/// Returns a single email template by ID, or null if not found.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal class GetEmailTemplateByIdQueryHandler : IQueryHandler<GetEmailTemplateByIdQuery, EmailTemplateDto?>
{
    private readonly IEmailTemplateRepository _repository;

    public GetEmailTemplateByIdQueryHandler(IEmailTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<EmailTemplateDto?> Handle(GetEmailTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var template = await _repository.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return template != null
            ? new EmailTemplateDto(
                template.Id,
                template.Name,
                template.Locale,
                template.Subject,
                template.HtmlBody,
                template.Version,
                template.IsActive,
                template.LastModifiedBy,
                template.CreatedAt,
                template.UpdatedAt)
            : null;
    }
}
