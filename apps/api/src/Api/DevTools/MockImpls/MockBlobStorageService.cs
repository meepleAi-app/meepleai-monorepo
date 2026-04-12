using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.Services.Pdf;

namespace Api.DevTools.MockImpls;

/// <summary>
/// In-memory mock of IBlobStorageService. Stores blobs in a ConcurrentDictionary
/// keyed by "gameId/fileId". For unknown keys (e.g. stale Quartz jobs persisted
/// in Postgres that reference blobs uploaded in a previous dev session) returns
/// a minimal valid PDF placeholder instead of null — this prevents the PDF
/// processing pipeline from crashing on orphaned jobs and is consistent with
/// the "everything is fake by default" philosophy of dev mocks.
/// Pre-signed URLs return a synthetic localhost URL.
/// No external S3/filesystem calls are made.
/// </summary>
internal sealed class MockBlobStorageService : IBlobStorageService
{
    // Minimal valid PDF header — enough to pass file type sniffing and stream reads.
    // The real PDF content does not matter because the PDF extractors are also mocked
    // and ignore the stream bytes entirely.
    private static readonly byte[] PlaceholderPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

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

        // Unknown key: return a minimal PDF placeholder so stale jobs don't crash
        // the pipeline. See class-level remarks.
        Stream placeholder = new MemoryStream(PlaceholderPdfBytes, writable: false);
        return Task.FromResult<Stream?>(placeholder);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        // Return true even for unknown keys — the mock semantics are "everything
        // is present", so "delete unknown" is a no-op success.
        _store.TryRemove(KeyOf(gameId, fileId), out _);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public string GetStoragePath(string fileId, string gameId, string fileName)
        => $"mock://{gameId}/{fileId}/{fileName}";

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
    {
        // Everything "exists" in mock mode.
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<string?> GetPresignedDownloadUrlAsync(string fileId, string gameId, int? expirySeconds = null)
    {
        // Always return a synthetic URL — the mock assumes every blob exists.
        return Task.FromResult<string?>(
            $"http://localhost/mock-s3/{gameId}/{fileId}?expiry={expirySeconds ?? 3600}");
    }
}
