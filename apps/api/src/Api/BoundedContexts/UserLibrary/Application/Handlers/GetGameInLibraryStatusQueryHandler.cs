using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for checking if a game is in user's library.
/// </summary>
internal class GetGameInLibraryStatusQueryHandler : IQueryHandler<GetGameInLibraryStatusQuery, GameInLibraryStatusDto>
{
    private readonly IUserLibraryRepository _libraryRepository;

    public GetGameInLibraryStatusQueryHandler(IUserLibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
    }

    public async Task<GameInLibraryStatusDto> Handle(GetGameInLibraryStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = await _libraryRepository.GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        return new GameInLibraryStatusDto(
            InLibrary: entry != null,
            IsFavorite: entry?.IsFavorite ?? false
        );
    }
}
