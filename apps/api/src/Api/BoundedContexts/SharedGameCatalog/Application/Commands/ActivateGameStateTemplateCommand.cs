using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to activate a game state template version.
/// Only one template per game can be active at a time.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
/// <param name="TemplateId">The ID of the template to activate</param>
internal record ActivateGameStateTemplateCommand(
    Guid TemplateId
) : ICommand<GameStateTemplateDto>;
