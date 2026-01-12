using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing erratum for a shared game.
/// Updates the description, page reference, and published date.
/// </summary>
internal record UpdateGameErrataCommand(
    Guid ErrataId,
    string Description,
    string PageReference,
    DateTime PublishedDate
) : ICommand<Unit>;
