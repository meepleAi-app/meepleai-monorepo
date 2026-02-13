using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting aggregated associated data counts for multiple collection entries.
/// Used for bulk removal warnings.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal class GetBulkCollectionAssociatedDataQueryHandler
    : IQueryHandler<GetBulkCollectionAssociatedDataQuery, BulkAssociatedDataDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IChatSessionRepository _chatSessionRepository;

    public GetBulkCollectionAssociatedDataQueryHandler(
        IUserLibraryRepository libraryRepository,
        IChatSessionRepository chatSessionRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _chatSessionRepository = chatSessionRepository ?? throw new ArgumentNullException(nameof(chatSessionRepository));
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

        // Aggregate counts across all game entities
        var totalCustomAgents = 0;
        var totalPrivatePdfs = 0;
        var totalChatSessions = 0;
        var totalGameSessions = 0;
        var totalChecklistItems = 0;
        var totalLabels = 0;

        foreach (var entityId in query.EntityIds)
        {
            // Get library entry with stats
            var entry = await _libraryRepository
                .GetUserGameWithStatsAsync(query.UserId, entityId, cancellationToken)
                .ConfigureAwait(false);

            if (entry == null)
                continue;

            // Aggregate counts
            if (entry.CustomAgentConfig != null)
                totalCustomAgents++;

            if (entry.HasPrivatePdf)
                totalPrivatePdfs++;

            var chatSessionsCount = await _chatSessionRepository
                .CountByUserAndGameAsync(query.UserId, entityId, cancellationToken)
                .ConfigureAwait(false);
            totalChatSessions += chatSessionsCount;

            totalGameSessions += entry.Sessions.Count;
            totalChecklistItems += entry.Checklist.Count;
            totalLabels += entry.Labels.Count;
        }

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
