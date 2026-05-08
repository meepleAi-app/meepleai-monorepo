using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;

/// <summary>
/// Handler for <see cref="GetUserGamebooksQuery"/> (Issue #869).
///
/// MVP scope (Phase 1):
///   Returns one card per PrivateGame owned by the user. Counts and status
///   are placeholders pending cross-BC composition (see DTO XML doc).
/// </summary>
internal sealed class GetUserGamebooksQueryHandler
    : IQueryHandler<GetUserGamebooksQuery, IReadOnlyList<GamebookCardDataDto>>
{
    private readonly IPrivateGameRepository _privateGameRepository;

    public GetUserGamebooksQueryHandler(IPrivateGameRepository privateGameRepository)
    {
        _privateGameRepository = privateGameRepository
            ?? throw new ArgumentNullException(nameof(privateGameRepository));
    }

    public async Task<IReadOnlyList<GamebookCardDataDto>> Handle(
        GetUserGamebooksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var games = await _privateGameRepository
            .GetByOwnerIdAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return games
            .OrderByDescending(g => g.UpdatedAt ?? g.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    private static GamebookCardDataDto MapToDto(PrivateGame game) => new(
        Id: game.Id,
        GameId: game.Id,
        Title: game.Title,
        Publisher: null,
        Year: game.YearPublished,
        Pages: 0,
        TotalPages: 0,
        Chunks: 0,
        Status: "ready",
        Cover: game.ImageUrl,
        Emoji: null,
        QaCount: 0,
        SessionsCount: 0,
        ErrorMsg: null);
}
