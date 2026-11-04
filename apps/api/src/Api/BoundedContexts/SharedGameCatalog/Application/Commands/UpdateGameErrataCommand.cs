using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing erratum for a shared game.
/// Updates the description, page reference, and published date.
/// </summary>
/// <remarks>#4543: <c>SharedGameId</c> è il gioco del segmento <c>{id}</c> della rotta; un'errata di un
/// altro gioco risponde 404.</remarks>
internal record UpdateGameErrataCommand(
    Guid SharedGameId,
    Guid ErrataId,
    string Description,
    string PageReference,
    DateTime PublishedDate
) : ICommand<Unit>;
