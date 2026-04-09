using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.Services.Pdf;

namespace Api.DevTools.MockImpls;

/// <summary>
/// In-memory mock of IBlobStorageService. Stores blobs in a ConcurrentDictionary
/// keyed by "gameId/fileId". Pre-signed URLs return a synthetic localhost URL.
/// No external S3/filesystem calls are made.
/// </summary>
internal sealed class MockBlobStorageService : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    private static string KeyOf(string gameId, string fileId) => $"{gameId}/{fileId}";

    /// <inheritdoc />
    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        var bytes = ms.ToArray();
        var fileId = $"MOCK-{Guid.NewGuid():N}";
        _store[KeyOf(gameId, fileId)] = bytes;
        return new BlobStorageResult(
            Success: true,
            FileId: fileId,
            FilePath: $"mock://{gameId}/{fileId}/{fileName}",
            FileSizeBytes: bytes.LongLength);
    }

    /// <inheritdoc />
    public Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(KeyOf(gameId, fileId), out var bytes))
        {
            Stream s = new MemoryStream(bytes, writable: false);
            return Task.FromResult<Stream?>(s);
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
        => Task.FromResult(_store.TryRemove(KeyOf(gameId, fileId), out _));

    /// <inheritdoc />
    public string GetStoragePath(string fileId, string gameId, string fileName)
        => $"mock://{gameId}/{fileId}/{fileName}";

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(KeyOf(gameId, fileId)));

    /// <inheritdoc />
    public Task<string?> GetPresignedDownloadUrlAsync(string fileId, string gameId, int? expirySeconds = null)
    {
        if (!_store.ContainsKey(KeyOf(gameId, fileId)))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(
            $"http://localhost/mock-s3/{gameId}/{fileId}?expiry={expirySeconds ?? 3600}");
    }
}
