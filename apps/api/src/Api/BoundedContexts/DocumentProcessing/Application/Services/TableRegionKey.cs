using System;
using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3435 SP4 idempotency keys. The image-region row id is regenerated on every replace-by-pdf
/// re-seed, so per-region state is keyed on a deterministic hash of (pdf, page, quantized bbox)
/// instead — stable as long as the bbox values are, and independent of the row id. The table
/// chunk's id is derived from the same hash so re-indexing a region replaces its one chunk.
/// </summary>
public static class TableRegionKey
{
    /// <summary>Deterministic hex hash over (pdf, page, bbox quantized to 1e-4). 64 chars.</summary>
    public static string ComputeRegionHash(Guid pdfId, int page, double x, double y, double width, double height)
    {
        static long Q(double v) => (long)Math.Round(Math.Clamp(v, 0d, 1d) * 10000d);
        var raw = $"{pdfId:N}:{page}:{Q(x)}:{Q(y)}:{Q(width)}:{Q(height)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Deterministic table-chunk Guid derived from the region hash (idempotent upsert key).</summary>
    public static Guid ChunkIdFromRegionHash(string regionHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("table-chunk:" + regionHash));
        return new Guid(bytes.AsSpan(0, 16).ToArray());
    }
}
