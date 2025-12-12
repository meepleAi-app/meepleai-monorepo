using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get document collection for a specific game.
/// Issue #2051: Get collection by game ID
/// </summary>
public record GetCollectionByGameQuery(
    Guid GameId
) : IQuery<DocumentCollectionDto?>;
