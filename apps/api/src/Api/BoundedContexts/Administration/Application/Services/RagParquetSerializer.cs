using Api.BoundedContexts.Administration.Application.DTOs;
using Parquet.Serialization;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Serializes and deserializes RAG export data (chunks and embeddings) to/from Parquet format.
/// Uses Parquet.Net class-based serialization with dedicated row types for schema clarity.
/// Vectors are stored as raw bytes (float[] → byte[]) for portability.
/// </summary>
internal static class RagParquetSerializer
{
    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Serializes a list of chunks to Parquet bytes.</summary>
    public static async Task<byte[]> SerializeChunksAsync(List<RagExportChunkLine> chunks)
    {
        var rows = chunks.Select(c => new ChunkRow
        {
            ChunkIndex = c.ChunkIndex,
            PageNumber = c.PageNumber,
            Content = c.Content,
            CharacterCount = c.CharacterCount
        }).ToList();

        using var ms = new MemoryStream();
        await ParquetSerializer.SerializeAsync(rows, ms).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>Deserializes Parquet bytes into a list of chunks.</summary>
    public static async Task<List<RagExportChunkLine>> DeserializeChunksAsync(byte[] parquetBytes)
    {
        using var ms = new MemoryStream(parquetBytes);
        var rows = new List<ChunkRow>();
        await foreach (var row in ParquetSerializer.DeserializeAllAsync<ChunkRow>(ms).ConfigureAwait(false))
            rows.Add(row);

        return rows.Select(r => new RagExportChunkLine(
            ChunkIndex: r.ChunkIndex,
            PageNumber: r.PageNumber,
            Content: r.Content,
            CharacterCount: r.CharacterCount)).ToList();
    }

    /// <summary>Serializes a list of embeddings to Parquet bytes.
    /// The float[] vector is stored as raw little-endian bytes for portability.</summary>
    public static async Task<byte[]> SerializeEmbeddingsAsync(List<RagExportEmbeddingLine> embeddings)
    {
        var rows = embeddings.Select(e => new EmbeddingRow
        {
            ChunkIndex = e.ChunkIndex,
            PageNumber = e.PageNumber,
            TextContent = e.TextContent,
            Vector = VectorToBytes(e.Vector),
            Model = e.Model
        }).ToList();

        using var ms = new MemoryStream();
        await ParquetSerializer.SerializeAsync(rows, ms).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>Deserializes Parquet bytes into a list of embeddings.
    /// The raw bytes are converted back to float[] vectors.</summary>
    public static async Task<List<RagExportEmbeddingLine>> DeserializeEmbeddingsAsync(byte[] parquetBytes)
    {
        using var ms = new MemoryStream(parquetBytes);
        var rows = new List<EmbeddingRow>();
        await foreach (var row in ParquetSerializer.DeserializeAllAsync<EmbeddingRow>(ms).ConfigureAwait(false))
            rows.Add(row);

        return rows.Select(r => new RagExportEmbeddingLine(
            ChunkIndex: r.ChunkIndex,
            PageNumber: r.PageNumber,
            TextContent: r.TextContent,
            Vector: BytesToVector(r.Vector),
            Model: r.Model)).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Converts a float[] to a raw little-endian byte array.</summary>
    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Converts a raw little-endian byte array back to float[].</summary>
    private static float[] BytesToVector(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private Parquet row types (used only for serialization schema)
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class ChunkRow
    {
        public int ChunkIndex { get; set; }
        public int? PageNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
    }

    private sealed class EmbeddingRow
    {
        public int ChunkIndex { get; set; }
        public int? PageNumber { get; set; }
        public string TextContent { get; set; } = string.Empty;
        public byte[] Vector { get; set; } = [];
        public string Model { get; set; } = string.Empty;
    }
}
