using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IRuleConflictFaqRepository.
/// Issue #3761: Conflict Resolution FAQ System.
/// </summary>
internal sealed class RuleConflictFaqRepository : RepositoryBase, IRuleConflictFaqRepository
{
    public RuleConflictFaqRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<RuleConflictFAQ?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var faqEntity = await DbContext.RuleConflictFAQs
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return faqEntity != null ? MapToDomain(faqEntity) : null;
    }

    public async Task<RuleConflictFAQ?> FindByPatternAsync(
        Guid gameId,
        string pattern,
        CancellationToken cancellationToken = default)
    {
        var normalizedPattern = pattern.Trim().ToLowerInvariant();

        var faqEntity = await DbContext.RuleConflictFAQs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.GameId == gameId && f.Pattern == normalizedPattern,
                cancellationToken)
            .ConfigureAwait(false);

        return faqEntity != null ? MapToDomain(faqEntity) : null;
    }

    public async Task<IReadOnlyList<RuleConflictFAQ>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var faqEntities = await DbContext.RuleConflictFAQs
            .AsNoTracking()
            .Where(f => f.GameId == gameId)
            .OrderByDescending(f => f.UsageCount)
            .ThenBy(f => f.Priority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return faqEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(RuleConflictFAQ faq, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(faq);
        await DbContext.RuleConflictFAQs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        CollectDomainEvents(faq);
    }

    public Task UpdateAsync(RuleConflictFAQ faq, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(faq);
        DbContext.RuleConflictFAQs.Update(entity);
        CollectDomainEvents(faq);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RuleConflictFAQs
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            throw new NotFoundException("RuleConflictFAQ", id.ToString());
        }

        DbContext.RuleConflictFAQs.Remove(entity);
    }

    private static RuleConflictFAQ MapToDomain(RuleConflictFAQEntity entity)
    {
        // Reconstruct using private constructor via reflection
        var faq = (RuleConflictFAQ)Activator.CreateInstance(typeof(RuleConflictFAQ), nonPublic: true)!;

        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.Id))!.SetValue(faq, entity.Id);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.GameId))!.SetValue(faq, entity.GameId);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.ConflictType))!.SetValue(faq, (ConflictType)entity.ConflictType);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.Pattern))!.SetValue(faq, entity.Pattern);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.Resolution))!.SetValue(faq, entity.Resolution);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.Priority))!.SetValue(faq, entity.Priority);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.UsageCount))!.SetValue(faq, entity.UsageCount);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.CreatedAt))!.SetValue(faq, entity.CreatedAt);
        typeof(RuleConflictFAQ).GetProperty(nameof(RuleConflictFAQ.UpdatedAt))!.SetValue(faq, entity.UpdatedAt);

        return faq;
    }

    private static RuleConflictFAQEntity MapToEntity(RuleConflictFAQ faq)
    {
        return new RuleConflictFAQEntity
        {
            Id = faq.Id,
            GameId = faq.GameId,
            ConflictType = (int)faq.ConflictType,
            Pattern = faq.Pattern,
            Resolution = faq.Resolution,
            Priority = faq.Priority,
            UsageCount = faq.UsageCount,
            CreatedAt = faq.CreatedAt,
            UpdatedAt = faq.UpdatedAt
        };
    }
}
