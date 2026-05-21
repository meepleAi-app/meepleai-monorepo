using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class UpdateGameBookCommandHandler : ICommandHandler<UpdateGameBookCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(uow);
        _repo = repo;
        _uow = uow;
    }

    public async Task<GameBookDto> Handle(UpdateGameBookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var book = await _repo.GetByIdAsync(command.BookId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", command.BookId.ToString());

        book.Rename(command.DisplayName, command.RequestedBy);
        book.UpdateRoles((GameBookRole)command.Roles, command.RequestedBy);

        await _repo.UpdateAsync(book, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return book.ToDto();
    }
}
