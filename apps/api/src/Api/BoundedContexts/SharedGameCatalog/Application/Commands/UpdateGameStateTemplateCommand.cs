using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update a game state template's schema.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
/// <param name="TemplateId">The ID of the template to update</param>
/// <param name="Name">Optional new name for the template</param>
/// <param name="SchemaJson">The new JSON schema</param>
/// <param name="NewVersion">The new version number</param>
internal record UpdateGameStateTemplateCommand(
    Guid TemplateId,
    string? Name,
    string SchemaJson,
    string NewVersion
) : ICommand<GameStateTemplateDto>;
