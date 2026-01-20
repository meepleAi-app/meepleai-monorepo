using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to bulk import multiple games from BoardGameGeek or CSV data.
/// Processes imports in parallel with rate limiting and returns success/failure summary.
/// </summary>
/// <param name="Games">List of games to import</param>
/// <param name="UserId">ID of the user performing the import (will be set as CreatedBy)</param>
public record BulkImportGamesCommand(
    List<BulkGameImportDto> Games,
    Guid UserId
) : ICommand<BulkImportResultDto>;

/// <summary>
/// DTO for bulk import input.
/// Can contain manual data or just BGG IDs for automated import.
/// </summary>
public record BulkGameImportDto(
    int? BggId,
    string? Title,
    int? YearPublished,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge
);

/// <summary>
/// Result summary for bulk import operation.
/// </summary>
public record BulkImportResultDto(
    int SuccessCount,
    int FailureCount,
    List<string> Errors,
    List<Guid> ImportedGameIds
);
