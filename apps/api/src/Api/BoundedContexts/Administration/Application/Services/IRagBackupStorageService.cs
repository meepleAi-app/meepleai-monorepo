using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Abstraction for RAG backup storage supporting dual-write (local + S3).
/// Local storage is always written; S3 is optional and failures are non-blocking.
/// </summary>
internal interface IRagBackupStorageService
{
    /// <summary>Writes a file to backup storage at the given relative path.</summary>
    Task WriteFileAsync(string relativePath, byte[] content, CancellationToken ct = default);

    /// <summary>Writes a file to backup storage from a stream at the given relative path.</summary>
    Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default);

    /// <summary>Reads a file from backup storage. Returns null if not found.</summary>
    Task<byte[]?> ReadFileAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Lists all snapshot IDs that have a manifest.json present in the local backup directory.
    /// </summary>
    Task<List<string>> ListSnapshotsAsync(CancellationToken ct = default);

    /// <summary>Deletes a snapshot directory (and all contents) from local storage.</summary>
    Task DeleteSnapshotAsync(string snapshotId, CancellationToken ct = default);

    /// <summary>
    /// Generates a pre-signed download URL for the given relative path via S3.
    /// Returns null when S3 is not configured or the object does not exist.
    /// </summary>
    Task<string?> GetDownloadUrlAsync(string relativePath, int expirySeconds = 3600, CancellationToken ct = default);
}

/// <summary>
/// Static path helpers for RAG backup directory and file locations.
/// All paths are relative to the backup root (e.g. "rag-exports/").
/// </summary>
internal static class RagBackupPathHelper
{
    /// <summary>
    /// Returns the base directory path for a snapshot.
    /// Example: "rag-exports/2026-03-28T12-00-00Z"
    /// </summary>
    public static string BuildSnapshotBasePath(string snapshotId) =>
        $"rag-exports/{snapshotId}";

    /// <summary>
    /// Returns the path for a single PDF document within a snapshot.
    /// Example: "rag-exports/2026-03-28T12-00-00Z/games/ark-nova/3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// </summary>
    public static string BuildDocumentPath(string snapshotId, string gameSlug, Guid pdfDocumentId) =>
        $"rag-exports/{snapshotId}/games/{gameSlug}/{pdfDocumentId}";

    /// <summary>
    /// Converts a game name to a URL-safe slug.
    /// Examples:
    ///   "Ark Nova"              → "ark-nova"
    ///   "7 Wonders Duel"        → "7-wonders-duel"
    ///   "King's Dilemma"        → "kings-dilemma"
    ///   "Ticket to Ride: Europe"→ "ticket-to-ride-europe"
    /// </summary>
    public static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unknown";

        // Normalize Unicode (decompose accented characters, e.g. é → e + combining accent)
        var normalized = name.Normalize(System.Text.NormalizationForm.FormD);

        // Remove non-ASCII characters (covers accents after decomposition)
        var asciiOnly = Regex.Replace(normalized, @"[^\u0000-\u007F]", string.Empty,
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Lowercase
        var lower = asciiOnly.ToLowerInvariant();

        // Replace any character that is not alphanumeric or whitespace with nothing
        var noSpecial = Regex.Replace(lower, @"[^a-z0-9\s]", string.Empty,
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Replace whitespace sequences with a single hyphen
        var hyphenated = Regex.Replace(noSpecial.Trim(), @"\s+", "-",
            RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // Remove leading/trailing hyphens (edge cases)
        var slug = hyphenated.Trim('-');

        return string.IsNullOrEmpty(slug) ? "unknown" : slug;
    }
}
