using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal sealed class GetGameBookByIdQueryHandler : IQueryHandler<GetGameBookByIdQuery, GameBookDto>
{
    private readonly IGameBookRepository _repo;

    public GetGameBookByIdQueryHandler(IGameBookRepository repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        _repo = repo;
    }

    public async Task<GameBookDto> Handle(GetGameBookByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var book = await _repo.GetByIdAsync(query.BookId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", query.BookId.ToString());

        return book.ToDto();
    }
}
