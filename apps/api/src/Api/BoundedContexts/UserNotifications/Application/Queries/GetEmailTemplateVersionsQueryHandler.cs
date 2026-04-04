using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetEmailTemplateVersionsQuery.
/// Returns all versions of a template by name, optionally filtered by locale.
/// Issue #54: CQRS queries for admin email template management.
/// </summary>
internal class GetEmailTemplateVersionsQueryHandler : IQueryHandler<GetEmailTemplateVersionsQuery, List<EmailTemplateDto>>
{
    private readonly IEmailTemplateRepository _repository;

    public GetEmailTemplateVersionsQueryHandler(IEmailTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<EmailTemplateDto>> Handle(GetEmailTemplateVersionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var templates = await _repository.GetByNameAsync(query.Name, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(query.Locale))
        {
            var normalizedLocale = query.Locale.Trim().ToLowerInvariant();
            templates = templates.Where(t => string.Equals(t.Locale, normalizedLocale, StringComparison.Ordinal)).ToList();
        }

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
