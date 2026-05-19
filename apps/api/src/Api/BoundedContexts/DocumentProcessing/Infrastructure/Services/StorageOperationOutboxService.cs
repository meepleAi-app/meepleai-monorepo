using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// EF-backed implementation of <see cref="IStorageOperationOutboxService"/>.
/// Inserts <see cref="StorageOperationOutboxEntity"/> rows; duplicate
/// <c>LegacyKey</c> values are swallowed (UNIQUE constraint violation =
/// no-op, the move is already enqueued / completed).
/// </summary>
internal sealed class StorageOperationOutboxService : IStorageOperationOutboxService
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StorageOperationOutboxService> _logger;

    public StorageOperationOutboxService(
        MeepleAiDbContext db,
        TimeProvider timeProvider,
        ILogger<StorageOperationOutboxService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> EnqueueAsync(
        Guid migrationId,
        string legacyKey,
        BlobCategory category,
        string resourceKey,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        // Derive the target NewKey by replacing the legacy prefix with the
        // category prefix. The legacy key shape is `pdf_uploads/{resourceKey}/{fileId}_{filename}`;
        // the new key shape is `{category.ToS3Folder()}/{resourceKey}/{fileId}_{filename}`.
        var newKey = DeriveNewKey(legacyKey, category);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var row = new StorageOperationOutboxEntity
        {
            Id = Guid.NewGuid(),
            MigrationId = migrationId,
            LegacyKey = legacyKey,
            NewKey = newKey,
            Category = category.ToString(),
            ResourceKey = resourceKey,
            ScheduledAt = scheduledAt ?? now,
            CreatedAt = now,
            Status = "Pending",
        };

        _db.StorageOperationOutbox.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex)
            when (IsUniqueViolation(ex))
        {
            // Idempotent path: another caller already enqueued this legacy key.
            _db.Entry(row).State = EntityState.Detached;
            _logger.LogDebug(
                ex,
                "Storage outbox: duplicate LegacyKey '{LegacyKey}' (migration {MigrationId}); treating as no-op",
                legacyKey, migrationId);
            return false;
        }
    }

    /// <summary>
    /// Replaces the leading <c>pdf_uploads</c> segment with the categorized
    /// folder. If the legacy key does not start with the expected prefix
    /// (shouldn't happen post-PR 1) the original key is returned unchanged
    /// — the drainer will fail the move on attempt and log the malformed key.
    /// </summary>
    private static string DeriveNewKey(string legacyKey, BlobCategory category)
    {
        const string LegacyPrefix = "pdf_uploads/";
        if (!legacyKey.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            return legacyKey;
        }
        return string.Concat(category.ToS3Folder(), "/", legacyKey.AsSpan(LegacyPrefix.Length));
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Postgres unique-violation surfaces as SqlState 23505 inside the
        // inner exception. We avoid taking a hard dependency on Npgsql here
        // by sniffing the type name + message — good enough for an idempotent
        // dedup path.
        var inner = ex.InnerException;
        if (inner is null)
        {
            return false;
        }

        var typeName = inner.GetType().FullName ?? string.Empty;
        var message = inner.Message ?? string.Empty;
        return typeName.Contains("PostgresException", StringComparison.OrdinalIgnoreCase)
            && message.Contains("IX_storage_operation_outbox_legacy_key", StringComparison.OrdinalIgnoreCase);
    }
}
