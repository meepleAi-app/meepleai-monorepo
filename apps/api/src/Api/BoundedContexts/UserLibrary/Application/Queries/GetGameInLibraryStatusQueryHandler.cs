using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for checking if a game is in the user's library and retrieving associated data counts.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
internal class GetGameInLibraryStatusQueryHandler : IQueryHandler<GetGameInLibraryStatusQuery, GameInLibraryStatusDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IChatSessionRepository _chatSessionRepository;

    public GetGameInLibraryStatusQueryHandler(
        IUserLibraryRepository libraryRepository,
        IChatSessionRepository chatSessionRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _chatSessionRepository = chatSessionRepository ?? throw new ArgumentNullException(nameof(chatSessionRepository));
    }

    public async Task<GameInLibraryStatusDto> Handle(
        GetGameInLibraryStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Check if game is in library with navigation properties loaded
        // Issue #4259: Use GetUserGameWithStatsAsync to ensure Sessions, Checklist, Labels are included
        var entry = await _libraryRepository
            .GetUserGameWithStatsAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        // Not in library
        if (entry == null)
        {
            return new GameInLibraryStatusDto(
                InLibrary: false,
                IsFavorite: false,
                AssociatedData: null
            );
        }

        // Calculate associated data counts
        var chatSessionsCount = await _chatSessionRepository
            .CountByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var associatedData = new AssociatedDataDto(
            HasCustomAgent: entry.CustomAgentConfig != null,
            HasPrivatePdf: entry.HasPrivatePdf,
            ChatSessionsCount: chatSessionsCount,
            GameSessionsCount: entry.Sessions.Count,
            ChecklistItemsCount: entry.Checklist.Count,
            LabelsCount: entry.Labels.Count
        );

        return new GameInLibraryStatusDto(
            InLibrary: true,
            IsFavorite: entry.IsFavorite,
            AssociatedData: associatedData
        );
    }
}
