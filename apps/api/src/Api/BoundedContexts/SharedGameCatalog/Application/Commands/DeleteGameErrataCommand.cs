using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete an existing erratum from a shared game.
/// This is a soft delete in the context of the aggregate - the erratum is removed from the collection.
/// </summary>
internal record DeleteGameErrataCommand(
    Guid ErrataId
) : ICommand<Unit>;
