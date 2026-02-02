using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to create a new game in the catalog.
/// Issue #2373: Added SharedGameId for catalog integration.
/// Issue #3372: Added PdfId for linking PDF during game creation.
/// </summary>
internal record CreateGameCommand(
    string Title,
    string? Publisher = null,
    int? YearPublished = null,
    int? MinPlayers = null,
    int? MaxPlayers = null,
    int? MinPlayTimeMinutes = null,
    int? MaxPlayTimeMinutes = null,
    string? IconUrl = null,
    string? ImageUrl = null,
    int? BggId = null,
    Guid? SharedGameId = null,
    Guid? PdfId = null
) : ICommand<GameDto>;
