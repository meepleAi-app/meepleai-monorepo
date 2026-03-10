using Api.BoundedContexts.UserNotifications.Domain.Aggregates;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

/// <summary>
/// Repository interface for EmailTemplate aggregate persistence.
/// Issue #52: P4.1 Domain entity for admin email template management.
/// </summary>
internal interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmailTemplate?> GetActiveAsync(string name, string locale, CancellationToken cancellationToken = default);
    Task<List<EmailTemplate>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<EmailTemplate>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<EmailTemplate>> GetAllAsync(string? type, string? locale, CancellationToken cancellationToken = default);
    Task<int> GetNextVersionAsync(string name, string locale, CancellationToken cancellationToken = default);
    Task AddAsync(EmailTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmailTemplate template, CancellationToken cancellationToken = default);
}
