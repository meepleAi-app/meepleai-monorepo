using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Labels;

/// <summary>
/// Handler for getting labels assigned to a specific game in the user's library.
/// </summary>
internal class GetGameLabelsQueryHandler : IQueryHandler<GetGameLabelsQuery, IReadOnlyList<LabelDto>>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IGameLabelRepository _labelRepository;

    public GetGameLabelsQueryHandler(
        IUserLibraryRepository libraryRepository,
        IGameLabelRepository labelRepository)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
    }

    public async Task<IReadOnlyList<LabelDto>> Handle(GetGameLabelsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Verify the game is in the user's library
        var entry = await _libraryRepository.GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Game is not in your library");

        // Get labels for this entry
        var labels = await _labelRepository.GetLabelsForEntryAsync(entry.Id, cancellationToken)
            .ConfigureAwait(false);

        return labels.Select(l => new LabelDto(
            Id: l.Id,
            Name: l.Name,
            Color: l.Color,
            IsPredefined: l.IsPredefined,
            CreatedAt: l.CreatedAt
        )).ToList();
    }
}
