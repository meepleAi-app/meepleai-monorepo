using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enqueue multiple BGG games from JSON file for batch import
/// Uses best-effort strategy: skips duplicates, continues on individual failures
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
public record EnqueueBggBatchFromJsonCommand : IRequest<BulkImportResult>
{
    /// <summary>
    /// JSON content with format: [{"bggId": 123, "name": "Game Name"}, ...]
    /// Max size: 10MB (approximately 50,000 games)
    /// </summary>
    public required string JsonContent { get; init; }

    /// <summary>
    /// User ID performing the import (for audit trail)
    /// </summary>
    public required Guid UserId { get; init; }
}
