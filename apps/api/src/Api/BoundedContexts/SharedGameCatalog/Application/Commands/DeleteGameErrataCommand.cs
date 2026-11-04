using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete an existing erratum from a shared game.
/// This is a soft delete in the context of the aggregate - the erratum is removed from the collection.
/// </summary>
/// <remarks>#4543: <c>SharedGameId</c> è il gioco del segmento <c>{id}</c> della rotta; un'errata di un
/// altro gioco risponde 404.</remarks>
internal record DeleteGameErrataCommand(
    Guid SharedGameId,
    Guid ErrataId
) : ICommand<Unit>;
