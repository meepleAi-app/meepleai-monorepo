using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting aggregated associated data counts for multiple collection entries.
/// Used for bulk removal warnings.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// Performance: Uses batch queries to avoid N+1 problem (2-3 queries vs 100 for 50 entities)
/// </summary>
internal class GetBulkCollectionAssociatedDataQueryHandler
    : IQueryHandler<GetBulkCollectionAssociatedDataQuery, BulkAssociatedDataDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetBulkCollectionAssociatedDataQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BulkAssociatedDataDto> Handle(
        GetBulkCollectionAssociatedDataQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Phase 3: Only Game entities have associated data
        // For other entity types (Player, Event, Session, etc.), return zeros
        if (query.EntityType != EntityType.Game)
        {
            return new BulkAssociatedDataDto(
                TotalCustomAgents: 0,
                TotalPrivatePdfs: 0,
                TotalChatSessions: 0,
                TotalGameSessions: 0,
                TotalChecklistItems: 0,
                TotalLabels: 0
            );
        }

        // Performance optimization: Batch query to avoid N+1 problem
        // Single query loads all entries with navigation properties (2-3 queries vs 100 for 50 entities)
        var entries = await _dbContext.UserLibraryEntries
            .Include(e => e.Sessions)
            .Include(e => e.Checklist)
            .Include(e => e.Labels)
            .Where(e => e.UserId == query.UserId && query.EntityIds.Contains(e.GameId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Aggregate counts
        var totalCustomAgents = entries.Count(e => !string.IsNullOrEmpty(e.CustomAgentConfigJson));
        var totalPrivatePdfs = entries.Count(e => e.PrivatePdfId.HasValue);
        var totalGameSessions = entries.Sum(e => e.Sessions?.Count ?? 0);
        var totalChecklistItems = entries.Sum(e => e.Checklist?.Count ?? 0);
        var totalLabels = entries.Sum(e => e.Labels?.Count ?? 0);

        // Batch chat sessions count (single query for all game IDs)
        var gameIds = entries.Select(e => e.GameId).ToList();
        var totalChatSessions = gameIds.Count > 0
            ? await _dbContext.ChatSessions
                .Where(cs => cs.UserId == query.UserId && gameIds.Contains(cs.GameId))
                .CountAsync(cancellationToken)
                .ConfigureAwait(false)
            : 0;

        return new BulkAssociatedDataDto(
            TotalCustomAgents: totalCustomAgents,
            TotalPrivatePdfs: totalPrivatePdfs,
            TotalChatSessions: totalChatSessions,
            TotalGameSessions: totalGameSessions,
            TotalChecklistItems: totalChecklistItems,
            TotalLabels: totalLabels
        );
    }
}
