using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles retrieval of version timeline with filtering and branching support.
/// </summary>
internal class GetVersionTimelineQueryHandler : IQueryHandler<GetVersionTimelineQuery, VersionTimelineDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetVersionTimelineQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    private sealed record RawVersionData(Guid Id, string Version, DateTime CreatedAt, Guid? ParentVersionId, string MergedFromVersionIds, int AtomCount, string AuthorName);

    public async Task<VersionTimelineDto> Handle(GetVersionTimelineQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var dbQuery = _dbContext.RuleSpecs
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .Where(r => r.GameId == query.GameId);

        dbQuery = ApplyFilters(dbQuery, query);

        var versions = await GetRawVersionsAsync(dbQuery, cancellationToken).ConfigureAwait(false);
        var versionNodes = BuildVersionNodes(versions);

        MarkCurrentVersion(versionNodes);

        var authors = GetAuthors(versionNodes);

        return new VersionTimelineDto(
            GameId: query.GameId,
            Versions: versionNodes,
            TotalVersions: versionNodes.Count,
            Authors: authors
        );
    }

    private IQueryable<RuleSpecEntity> ApplyFilters(IQueryable<RuleSpecEntity> dbQuery, GetVersionTimelineQuery query)
    {
        if (query.StartDate.HasValue)
            dbQuery = dbQuery.Where(r => r.CreatedAt >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            dbQuery = dbQuery.Where(r => r.CreatedAt <= query.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            var authorFilter = query.Author.Trim();
            dbQuery = dbQuery.Where(r => r.CreatedBy != null &&
                ((r.CreatedBy.DisplayName != null && r.CreatedBy.DisplayName.Contains(authorFilter)) ||
                 (r.CreatedBy.Email != null && r.CreatedBy.Email.Contains(authorFilter))));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            var searchTerm = query.SearchQuery.Trim();
            dbQuery = dbQuery.Where(r => r.Version.Contains(searchTerm));
        }

        return dbQuery;
    }

    private async Task<List<RawVersionData>> GetRawVersionsAsync(IQueryable<RuleSpecEntity> dbQuery, CancellationToken ct)
    {
        return await dbQuery
            .OrderBy(r => r.CreatedAt)
            .Select(r => new RawVersionData(
                r.Id,
                r.Version,
                r.CreatedAt,
                r.ParentVersionId,
                r.MergedFromVersionIds ?? string.Empty,
                r.Atoms.Count,
                r.CreatedBy != null
                    ? r.CreatedBy.DisplayName ?? r.CreatedBy.Email ?? "Unknown"
                    : "Unknown"
            ))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private List<VersionNodeDto> BuildVersionNodes(List<RawVersionData> versions)
    {
        var versionNodes = new List<VersionNodeDto>();
        var versionMap = versions.ToDictionary(v => v.Id, v => v.Version);

        foreach (var version in versions)
        {
            var mergedFromIds = !string.IsNullOrWhiteSpace(version.MergedFromVersionIds)
                ? version.MergedFromVersionIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => Guid.TryParse(id.Trim(), out var guid) ? guid : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList()
                : new List<Guid>();

            var node = new VersionNodeDto(
                Id: version.Id,
                Version: version.Version,
                Title: $"Version {version.Version}",
                Description: $"{version.AtomCount} rule atoms",
                Author: version.AuthorName,
                CreatedAt: version.CreatedAt,
                ParentVersionId: version.ParentVersionId,
                ParentVersion: version.ParentVersionId.HasValue && versionMap.TryGetValue(version.ParentVersionId.Value, out var parentVer)
                    ? parentVer
                    : null,
                MergedFromVersionIds: mergedFromIds,
                MergedFromVersions: mergedFromIds
                    .Where(id => versionMap.ContainsKey(id))
                    .Select(id => versionMap[id])
                    .ToList(),
                ChangeCount: version.AtomCount,
                IsCurrentVersion: false
            );

            versionNodes.Add(node);
        }

        return versionNodes;
    }

    private void MarkCurrentVersion(List<VersionNodeDto> versionNodes)
    {
        if (versionNodes.Count > 0)
        {
            var latestVersion = versionNodes.MaxBy(v => v.CreatedAt)!;
            for (int i = 0; i < versionNodes.Count; i++)
            {
                if (versionNodes[i].Id == latestVersion.Id)
                {
                    versionNodes[i] = versionNodes[i] with { IsCurrentVersion = true };
                    break;
                }
            }
        }
    }

    private List<string> GetAuthors(List<VersionNodeDto> versionNodes)
    {
        return versionNodes
            .Select(v => v.Author)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();
    }
}
