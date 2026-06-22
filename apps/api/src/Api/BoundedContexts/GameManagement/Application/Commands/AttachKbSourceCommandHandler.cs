using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class AttachKbSourceCommandHandler : ICommandHandler<AttachKbSourceCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public AttachKbSourceCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(uow);
        _repo = repo;
        _uow = uow;
    }

    public async Task<GameBookDto> Handle(AttachKbSourceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var book = await _repo.GetByIdAsync(command.BookId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", command.BookId.ToString());

        // Conflict check only for community books
        if (book.OwnerUserId is null)
        {
            var conflict = await _repo.FindCommunityByKbSourceAsync(command.PdfDocId, cancellationToken)
                .ConfigureAwait(false);
            if (conflict is not null && conflict.Id != book.Id)
            {
                throw new ConflictException(
                    $"PDF {command.PdfDocId} already kbSource of community book {conflict.Id}");
            }
        }

        book.AttachKbSource(command.PdfDocId, command.RequestedBy);

        await _repo.UpdateAsync(book, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return book.ToDto();
    }
}
