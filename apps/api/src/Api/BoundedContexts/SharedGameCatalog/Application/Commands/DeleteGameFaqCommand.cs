using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete an existing FAQ from a shared game.
/// This is a soft delete in the context of the aggregate - the FAQ is removed from the collection.
/// </summary>
/// <remarks>#4557: <c>SharedGameId</c> è il gioco del segmento <c>{id}</c> della rotta; una FAQ di un
/// altro gioco risponde 404.</remarks>
internal record DeleteGameFaqCommand(
    Guid SharedGameId,
    Guid FaqId
) : ICommand<Unit>;
