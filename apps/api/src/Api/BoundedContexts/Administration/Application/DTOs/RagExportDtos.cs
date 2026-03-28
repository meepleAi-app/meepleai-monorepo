using System.Text.Json.Serialization;

namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Global index file written as manifest.json at the snapshot root.
/// Lists every document included in the snapshot.
/// </summary>
internal sealed record RagExportManifest(
    [property: JsonPropertyName("exportVersion")] string ExportVersion,
    [property: JsonPropertyName("exportedAt")] DateTimeOffset ExportedAt,
    [property: JsonPropertyName("totalDocuments")] int TotalDocuments,
    [property: JsonPropertyName("totalChunks")] int TotalChunks,
    [property: JsonPropertyName("totalEmbeddings")] int TotalEmbeddings,
    [property: JsonPropertyName("embeddingModel")] string EmbeddingModel,
    [property: JsonPropertyName("documents")] List<RagExportManifestEntry> Documents);

/// <summary>
/// One entry per document in the manifest's document list.
/// </summary>
internal sealed record RagExportManifestEntry(
    [property: JsonPropertyName("pdfDocumentId")] Guid PdfDocumentId,
    [property: JsonPropertyName("gameSlug")] string GameSlug,
    [property: JsonPropertyName("gameName")] string GameName,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("chunks")] int Chunks,
    [property: JsonPropertyName("language")] string Language);

/// <summary>
/// Per-document metadata file (metadata.json) written inside each document bundle directory.
/// </summary>
internal sealed record RagExportDocumentMetadata(
    [property: JsonPropertyName("exportVersion")] string ExportVersion,
    [property: JsonPropertyName("exportedAt")] DateTimeOffset ExportedAt,
    [property: JsonPropertyName("embeddingModel")] string EmbeddingModel,
    [property: JsonPropertyName("embeddingDimensions")] int EmbeddingDimensions,
    [property: JsonPropertyName("document")] RagExportDocumentInfo Document,
    [property: JsonPropertyName("stats")] RagExportDocumentStats Stats);

/// <summary>
/// Detailed information about the source PDF document included in the metadata file.
/// </summary>
internal sealed record RagExportDocumentInfo(
    [property: JsonPropertyName("pdfDocumentId")] Guid PdfDocumentId,
    [property: JsonPropertyName("gameId")] Guid? GameId,
    [property: JsonPropertyName("gameSlug")] string GameSlug,
    [property: JsonPropertyName("gameName")] string GameName,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("languageConfidence")] double? LanguageConfidence,
    [property: JsonPropertyName("documentCategory")] string? DocumentCategory,
    [property: JsonPropertyName("versionLabel")] string? VersionLabel,
    [property: JsonPropertyName("licenseType")] int? LicenseType,
    [property: JsonPropertyName("pageCount")] int? PageCount,
    [property: JsonPropertyName("characterCount")] int? CharacterCount,
    [property: JsonPropertyName("contentHash")] string? ContentHash,
    [property: JsonPropertyName("fileSizeBytes")] long? FileSizeBytes);

/// <summary>
/// Aggregate statistics for a single document bundle.
/// </summary>
internal sealed record RagExportDocumentStats(
    [property: JsonPropertyName("totalChunks")] int TotalChunks,
    [property: JsonPropertyName("totalEmbeddings")] int TotalEmbeddings,
    [property: JsonPropertyName("chunkSizeAvg")] int ChunkSizeAvg);

/// <summary>
/// A single line in the chunks.jsonl file for a document bundle.
/// </summary>
internal sealed record RagExportChunkLine(
    [property: JsonPropertyName("chunkIndex")] int ChunkIndex,
    [property: JsonPropertyName("pageNumber")] int? PageNumber,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("characterCount")] int CharacterCount);

/// <summary>
/// A single line in the embeddings.jsonl file for a document bundle.
/// </summary>
internal sealed record RagExportEmbeddingLine(
    [property: JsonPropertyName("chunkIndex")] int ChunkIndex,
    [property: JsonPropertyName("pageNumber")] int? PageNumber,
    [property: JsonPropertyName("textContent")] string TextContent,
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("model")] string Model);
