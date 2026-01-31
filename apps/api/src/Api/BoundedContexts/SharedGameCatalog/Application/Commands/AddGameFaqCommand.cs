using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to add a new FAQ to an existing shared game.
/// The FAQ is created with a specific order and will be visible immediately.
/// </summary>
internal record AddGameFaqCommand(
    Guid SharedGameId,
    string Question,
    string Answer,
    int Order
) : ICommand<Guid>;
