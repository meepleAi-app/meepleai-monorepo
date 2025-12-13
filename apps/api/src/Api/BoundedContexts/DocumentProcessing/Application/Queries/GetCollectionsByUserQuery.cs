using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get all document collections created by a specific user.
/// Issue #2051: Get collections by user ID
/// </summary>
public record GetCollectionsByUserQuery(
    Guid UserId
) : IQuery<IReadOnlyList<DocumentCollectionDto>>;
