using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class SoftDeleteGameBookCommandHandler : ICommandHandler<SoftDeleteGameBookCommand, Unit>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public SoftDeleteGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(uow);
        _repo = repo;
        _uow = uow;
    }

    public async Task<Unit> Handle(SoftDeleteGameBookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var book = await _repo.GetByIdAsync(command.BookId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", command.BookId.ToString());

        book.SoftDelete(command.RequestedBy);

        await _repo.UpdateAsync(book, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
