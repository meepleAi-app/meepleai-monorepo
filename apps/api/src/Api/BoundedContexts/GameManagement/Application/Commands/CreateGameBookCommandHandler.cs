using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class CreateGameBookCommandHandler : ICommandHandler<CreateGameBookCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(uow);
        _repo = repo;
        _uow = uow;
    }

    public async Task<GameBookDto> Handle(CreateGameBookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check kb_source conflict only for community books
        if (command.OwnerUserId is null && command.KbSourceDocId.HasValue)
        {
            var existing = await _repo.FindCommunityByKbSourceAsync(command.KbSourceDocId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                throw new ConflictException(
                    $"PDF {command.KbSourceDocId} already linked to community book {existing.Id}");
            }
        }

        var book = command.OwnerUserId.HasValue
            ? GameBook.CreatePersonal(
                command.GameRef, command.OwnerUserId.Value, command.DisplayName,
                (GameBookRole)command.Roles, (ParagraphScheme)command.ParagraphScheme,
                command.Language, command.SequentialRead, command.KbSourceDocId, command.PhysicalOnly)
            : GameBook.CreateCommunity(
                command.GameRef, command.DisplayName,
                (GameBookRole)command.Roles, (ParagraphScheme)command.ParagraphScheme,
                command.Language, command.SequentialRead, command.KbSourceDocId, command.PhysicalOnly,
                command.RequestedBy);

        await _repo.AddAsync(book, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return book.ToDto();
    }
}
