using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to add a new erratum to an existing shared game.
/// The erratum is created with a description, page reference, and published date.
/// </summary>
internal record AddGameErrataCommand(
    Guid SharedGameId,
    string Description,
    string PageReference,
    DateTime PublishedDate
) : ICommand<Guid>;
