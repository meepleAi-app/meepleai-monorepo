using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Implementation of storage quota management service.
/// Issue #2732: Storage quota tracking for user documents.
/// </summary>
internal sealed class StorageQuotaService : IStorageQuotaService
{
    private readonly IPdfDocumentRepository _documentRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageQuotaService> _logger;

    // Default quota: 1GB per user
    private const long DefaultMaxBytesPerUser = 1L * 1024 * 1024 * 1024; // 1 GB

    public StorageQuotaService(
        IPdfDocumentRepository documentRepo,
        IConfiguration configuration,
        ILogger<StorageQuotaService> logger)
    {
        _documentRepo = documentRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StorageQuotaStatus> GetUserQuota(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Get documents uploaded by user (database-level filtering for performance)
        var userDocuments = await _documentRepo.FindByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // Calculate total used storage
        var usedBytes = userDocuments.Sum(d => d.FileSize.Bytes);

        // Get max quota from configuration (with default fallback)
        var maxBytes = _configuration.GetValue<long?>("Storage:MaxBytesPerUser")
            ?? DefaultMaxBytesPerUser;

        _logger.LogDebug(
            "User {UserId} storage quota: {Used} / {Max} bytes ({Percent}%)",
            userId, usedBytes, maxBytes, (decimal)usedBytes / maxBytes * 100);

        return new StorageQuotaStatus
        {
            UsedBytes = usedBytes,
            MaxBytes = maxBytes
        };
    }

    public async Task<bool> CanUploadDocument(
        Guid userId,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        if (fileSize <= 0)
        {
            _logger.LogWarning("Invalid file size {FileSize} for quota check", fileSize);
            return false;
        }

        var quota = await GetUserQuota(userId, cancellationToken).ConfigureAwait(false);

        var wouldExceed = quota.UsedBytes + fileSize > quota.MaxBytes;

        if (wouldExceed)
        {
            _logger.LogWarning(
                "User {UserId} would exceed quota. Current: {Used}, FileSize: {FileSize}, Max: {Max}",
                userId, quota.UsedBytes, fileSize, quota.MaxBytes);
        }

        return !wouldExceed;
    }
}