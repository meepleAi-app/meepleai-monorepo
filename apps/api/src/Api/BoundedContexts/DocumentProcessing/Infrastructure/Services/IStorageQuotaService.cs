using Api.BoundedContexts.DocumentProcessing.Application.DTOs;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Service interface for managing storage quotas per user.
/// Issue #2732: Storage quota management for document uploads.
/// </summary>
public interface IStorageQuotaService
{
    /// <summary>
    /// Gets the storage quota status for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage quota status including used/max bytes and availability.</returns>
    Task<StorageQuotaStatus> GetUserQuota(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user can upload a document of the specified size.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user can upload the file; otherwise false.</returns>
    Task<bool> CanUploadDocument(
        Guid userId,
        long fileSize,
        CancellationToken cancellationToken = default);
}
