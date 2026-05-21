using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal sealed class ListGameBooksByGameQueryHandler
    : IQueryHandler<ListGameBooksByGameQuery, IReadOnlyList<GameBookDto>>
{
    private readonly IGameBookRepository _repo;

    public ListGameBooksByGameQueryHandler(IGameBookRepository repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        _repo = repo;
    }

    public async Task<IReadOnlyList<GameBookDto>> Handle(
        ListGameBooksByGameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var books = await _repo.ListByGameRefAsync(query.GameRef, query.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        return books.Select(b => b.ToDto()).ToList();
    }
}
