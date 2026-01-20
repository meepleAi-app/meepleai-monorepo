using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to import a board game from BoardGameGeek into the SharedGameCatalog.
/// Creates a new SharedGame in Draft status with metadata fetched from BGG API.
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to import</param>
/// <param name="UserId">ID of the user performing the import (will be set as CreatedBy)</param>
public record ImportGameFromBggCommand(int BggId, Guid UserId) : ICommand<Guid>;
