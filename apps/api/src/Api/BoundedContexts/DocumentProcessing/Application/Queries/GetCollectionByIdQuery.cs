using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get a document collection by its ID.
/// Issue #2051: Get collection by ID
/// </summary>
public record GetCollectionByIdQuery(
    Guid CollectionId
) : IQuery<DocumentCollectionDto?>;
