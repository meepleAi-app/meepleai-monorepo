using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete an existing FAQ from a shared game.
/// This is a soft delete in the context of the aggregate - the FAQ is removed from the collection.
/// </summary>
internal record DeleteGameFaqCommand(
    Guid FaqId
) : ICommand<Unit>;
