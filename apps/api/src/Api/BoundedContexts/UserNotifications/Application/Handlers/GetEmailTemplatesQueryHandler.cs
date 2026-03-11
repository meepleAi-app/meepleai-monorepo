using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetEmailTemplatesQuery.
/// Returns all templates with optional type and locale filtering.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal class GetEmailTemplatesQueryHandler : IQueryHandler<GetEmailTemplatesQuery, List<EmailTemplateDto>>
{
    private readonly IEmailTemplateRepository _repository;

    public GetEmailTemplatesQueryHandler(IEmailTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EmailTemplateDto>> Handle(GetEmailTemplatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var templates = await _repository.GetAllAsync(query.Type, query.Locale, cancellationToken).ConfigureAwait(false);

        return templates.Select(t => new EmailTemplateDto(
            t.Id,
            t.Name,
            t.Locale,
            t.Subject,
            t.HtmlBody,
            t.Version,
            t.IsActive,
            t.LastModifiedBy,
            t.CreatedAt,
            t.UpdatedAt)).ToList();
    }
}
