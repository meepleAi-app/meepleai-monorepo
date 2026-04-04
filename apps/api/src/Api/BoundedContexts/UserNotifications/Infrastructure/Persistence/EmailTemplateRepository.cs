using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of EmailTemplate repository.
/// Maps between domain EmailTemplate aggregate and EmailTemplateEntity persistence model.
/// Issue #52: P4.1 Domain entity for admin email template management.
/// </summary>
internal class EmailTemplateRepository : RepositoryBase, IEmailTemplateRepository
{
    public EmailTemplateRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<EmailTemplate?> GetActiveAsync(string name, string locale, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedLocale = locale.Trim().ToLowerInvariant();

        var entity = await DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IsActive && e.Name == normalizedName && e.Locale == normalizedLocale, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<EmailTemplate>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Locale)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<EmailTemplate>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLowerInvariant();

        var entities = await DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .Where(e => e.Name == normalizedName)
            .OrderByDescending(e => e.Version)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<EmailTemplate>> GetAllAsync(string? type, string? locale, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToLowerInvariant();
            query = query.Where(e => e.Name == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(locale))
        {
            var normalizedLocale = locale.Trim().ToLowerInvariant();
            query = query.Where(e => e.Locale == normalizedLocale);
        }

        var entities = await query
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Locale)
            .ThenByDescending(e => e.Version)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetNextVersionAsync(string name, string locale, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedLocale = locale.Trim().ToLowerInvariant();

        var maxVersion = await DbContext.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .Where(e => e.Name == normalizedName && e.Locale == normalizedLocale)
            .MaxAsync(e => (int?)e.Version, cancellationToken).ConfigureAwait(false);

        return (maxVersion ?? 0) + 1;
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(template);
        var entity = MapToPersistence(template);
        await DbContext.Set<EmailTemplateEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(template);
        var entity = MapToPersistence(template);

        var tracked = DbContext.ChangeTracker.Entries<EmailTemplateEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
        {
            tracked.CurrentValues.SetValues(entity);
            tracked.State = EntityState.Modified;
        }
        else
        {
            DbContext.Set<EmailTemplateEntity>().Update(entity);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static EmailTemplateEntity MapToPersistence(EmailTemplate template)
    {
        return new EmailTemplateEntity
        {
            Id = template.Id,
            Name = template.Name,
            Locale = template.Locale,
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            Version = template.Version,
            IsActive = template.IsActive,
            LastModifiedBy = template.LastModifiedBy,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private static EmailTemplate MapToDomain(EmailTemplateEntity entity)
    {
        return EmailTemplate.Reconstitute(
            id: entity.Id,
            name: entity.Name,
            locale: entity.Locale,
            subject: entity.Subject,
            htmlBody: entity.HtmlBody,
            version: entity.Version,
            isActive: entity.IsActive,
            lastModifiedBy: entity.LastModifiedBy,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt);
    }
}
