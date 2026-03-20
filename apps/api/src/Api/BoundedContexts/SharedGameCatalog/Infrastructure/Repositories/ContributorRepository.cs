using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Contributor entity.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
internal sealed class ContributorRepository : RepositoryBase, IContributorRepository
{

    public ContributorRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(Contributor contributor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        var entity = MapToEntity(contributor);
        await DbContext.Set<ContributorEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Contributor>> GetBySharedGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .Where(c => c.SharedGameId == sharedGameId)
            .OrderByDescending(c => c.IsPrimaryContributor)
            .ThenByDescending(c => c.Contributions.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<(List<Contributor> Contributors, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .Where(c => c.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var contributors = entities.Select(MapToDomain).ToList();

        return (contributors, totalCount);
    }

    public async Task<Contributor?> GetByIdAsync(
        Guid contributorId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .FirstOrDefaultAsync(c => c.Id == contributorId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public void Update(Contributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        var entity = MapToEntity(contributor);
        DbContext.Set<ContributorEntity>().Update(entity);
    }

    #region Mapping Methods

    private static Contributor MapToDomain(ContributorEntity entity)
    {
        var contributions = entity.Contributions?
            .Select(c => MapContributionRecordToDomain(c))
            .ToList();

        return new Contributor(
            entity.Id,
            entity.UserId,
            entity.SharedGameId,
            entity.IsPrimaryContributor,
            entity.CreatedAt,
            entity.ModifiedAt,
            contributions);
    }

    private static ContributionRecord MapContributionRecordToDomain(ContributionRecordEntity entity)
    {
        var documentIds = string.IsNullOrWhiteSpace(entity.DocumentIdsJson)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(entity.DocumentIdsJson) ?? new List<Guid>();

        return new ContributionRecord(
            entity.Id,
            entity.ContributorId,
            (ContributionRecordType)entity.Type,
            entity.Description,
            entity.Version,
            entity.ContributedAt,
            entity.ShareRequestId,
            documentIds,
            entity.IncludesGameData,
            entity.IncludesMetadata);
    }

    private static ContributorEntity MapToEntity(Contributor contributor)
    {
        return new ContributorEntity
        {
            Id = contributor.Id,
            UserId = contributor.UserId,
            SharedGameId = contributor.SharedGameId,
            IsPrimaryContributor = contributor.IsPrimaryContributor,
            CreatedAt = contributor.CreatedAt,
            ModifiedAt = contributor.ModifiedAt,
            Contributions = contributor.Contributions
                .Select(MapContributionRecordToEntity)
                .ToList()
        };
    }

    private static ContributionRecordEntity MapContributionRecordToEntity(ContributionRecord record)
    {
        var documentIdsJson = record.DocumentIds.Count > 0
            ? JsonSerializer.Serialize(record.DocumentIds)
            : null;

        return new ContributionRecordEntity
        {
            Id = record.Id,
            ContributorId = record.ContributorId,
            Type = (int)record.Type,
            Description = record.Description,
            Version = record.Version,
            ContributedAt = record.ContributedAt,
            ShareRequestId = record.ShareRequestId,
            DocumentIdsJson = documentIdsJson,
            IncludesGameData = record.IncludesGameData,
            IncludesMetadata = record.IncludesMetadata
        };
    }

    #endregion
}
