using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;

/// <summary>
/// Handler for GetUserContributionsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserContributionsQueryHandler : IRequestHandler<GetUserContributionsQuery, PagedResult<UserContributionDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetUserContributionsQueryHandler> _logger;

    public GetUserContributionsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetUserContributionsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<UserContributionDto>> Handle(
        GetUserContributionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting contributions for user {UserId}: Page={Page}, PageSize={PageSize}",
            query.UserId,
            query.PageNumber,
            query.PageSize);

        var dbQuery = _context.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.SharedGame)
            .Include(c => c.Contributions)
            .Where(c => c.UserId == query.UserId);

        // Order by most recent contribution
        dbQuery = dbQuery.OrderByDescending(c => c.Contributions.Max(cr => cr.ContributedAt));

        // Get total count
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination and project to DTOs
        var contributors = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var contributions = contributors.Select(c => MapToDto(c)).ToList();

        _logger.LogInformation(
            "Retrieved {Count} contributions (Total: {Total}) for user {UserId}",
            contributions.Count, total, query.UserId);

        return new PagedResult<UserContributionDto>(
            Items: contributions,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }

    private static UserContributionDto MapToDto(ContributorEntity contributor)
    {
        var orderedContributions = contributor.Contributions
            .OrderBy(cr => cr.ContributedAt)
            .ToList();

        var firstContribution = orderedContributions.FirstOrDefault();
        var lastContribution = orderedContributions.LastOrDefault();

        return new UserContributionDto(
            ContributorId: contributor.Id,
            SharedGameId: contributor.SharedGameId,
            GameTitle: contributor.SharedGame?.Title ?? "Unknown Game",
            GameThumbnailUrl: contributor.SharedGame?.ThumbnailUrl,
            IsPrimaryContributor: contributor.IsPrimaryContributor,
            ContributionCount: contributor.Contributions.Count,
            FirstContributionAt: firstContribution?.ContributedAt ?? contributor.CreatedAt,
            LastContributionAt: lastContribution?.ContributedAt ?? contributor.CreatedAt,
            Contributions: orderedContributions.Select(cr => MapContributionRecordToDto(cr)).ToList());
    }

    private static ContributionRecordDto MapContributionRecordToDto(ContributionRecordEntity record)
    {
        var documentCount = 0;
        if (!string.IsNullOrEmpty(record.DocumentIdsJson))
        {
            try
            {
                var documentIds = JsonSerializer.Deserialize<List<Guid>>(record.DocumentIdsJson);
                documentCount = documentIds?.Count ?? 0;
            }
            catch
            {
                // Ignore JSON parsing errors
            }
        }

        return new ContributionRecordDto(
            Id: record.Id,
            Type: (ContributionRecordType)record.Type,
            Description: record.Description,
            DocumentCount: documentCount,
            ContributedAt: record.ContributedAt);
    }
}
