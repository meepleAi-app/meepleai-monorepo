using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all documents for a shared game.
/// Optionally filter by document type.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="DocumentType">Optional filter by document type</param>
internal record GetDocumentsBySharedGameQuery(
    Guid SharedGameId,
    SharedGameDocumentType? DocumentType = null
) : IQuery<IReadOnlyList<SharedGameDocumentDto>>;
