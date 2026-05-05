using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to retrieve the current processing status of a photo batch.
/// Enforces ownership: only the batch owner may query the status.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.7.
/// </summary>
/// <param name="UserId">The authenticated user requesting the status.</param>
/// <param name="BatchId">The ID of the <see cref="Domain.Entities.PhotoBatchUpload"/> to inspect.</param>
internal sealed record GetPhotoBatchStatusQuery(Guid UserId, Guid BatchId)
    : IQuery<PhotoBatchStatusDto>;
