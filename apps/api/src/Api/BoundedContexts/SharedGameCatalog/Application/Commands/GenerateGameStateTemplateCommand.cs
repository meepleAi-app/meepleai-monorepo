using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to generate a game state template using AI analysis of rulebook.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="Name">Name for the template</param>
/// <param name="CreatedBy">The ID of the user creating the template</param>
/// <param name="SetAsActive">Whether to set this template as the active version</param>
internal record GenerateGameStateTemplateCommand(
    Guid SharedGameId,
    string Name,
    Guid CreatedBy,
    bool SetAsActive = false
) : ICommand<GameStateTemplateDto>;
