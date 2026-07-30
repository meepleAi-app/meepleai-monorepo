using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Models;

/// <summary>
/// Result of the RAG prompt assembly pipeline.
/// Contains the fully constructed system/user prompts with RAG context and chat history.
/// </summary>
internal sealed record AssembledPrompt(
    string SystemPrompt,
    string UserPrompt,
    List<ChunkCitation> Citations,
    int EstimatedTokens);

/// <summary>
/// Tracks which document chunks were used in the prompt (for debug/admin only).
/// New fields use default values for backward compatibility with existing call sites.
/// </summary>
internal sealed record ChunkCitation(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview,
    CopyrightTier CopyrightTier = CopyrightTier.Protected,
    string? ParaphrasedSnippet = null,
    bool IsPublic = false)
{
    /// <summary>
    /// Full chunk text used by the copyright leak guard (#447).
    /// In-memory only during request lifecycle — NOT persisted in CitationsJson
    /// (marked [JsonIgnore]). Null when rehydrated from storage (older messages).
    /// </summary>
    [JsonIgnore]
    public string? FullText { get; init; }

    /// <summary>
    /// Source game ID — populated on the cross-game path (AssembleFromContextAsync, #1661).
    /// Null on single-game paths (AssemblePromptAsync) to preserve backward compatibility
    /// with all existing call sites that create ChunkCitation without a game context.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// #2311 BE-1 — zero-based chunk position inside the source PDF. Combined with
    /// <see cref="DocumentId"/> it uniquely identifies the underlying TextChunk row
    /// (unique index <c>ix_text_chunks_pdf_chunk_index</c>), enabling the DEC-D2
    /// usage_count increment hook to resolve the chunk without an extra `ChunkId`
    /// roundtrip. Null on legacy JSON deserialization (older citations persisted
    /// before this field landed) — the increment hook treats absent values as a
    /// best-effort no-op.
    /// </summary>
    public int? ChunkIndex { get; init; }

    /// <summary>
    /// SP-C (#3407): raw bounding-box JSON of the cited chunk (<c>text_chunks.bounding_boxes_json</c>),
    /// carried from the vector arm. <see cref="JsonIgnoreAttribute"/> like <see cref="FullText"/>:
    /// transient per-request retrieval metadata that MUST NOT persist in <c>CitationsJson</c>. Raw bbox
    /// is verbatim-equivalent (highlights the exact cited text), so persisting it ungated would leak on
    /// history reload for Protected content (DA-4). The live path parses + Full-gates it into
    /// <c>CitationDto.Regions</c> at the handler boundary; reloaded citations fall back to the text-quote
    /// highlight (SP0 Pattern A).
    /// </summary>
    [JsonIgnore]
    public string? BoundingBoxesJson { get; init; }

    /// <summary>SP-C (#3407): chunk char offsets in the source PDF text. Transient ([JsonIgnore]) — see <see cref="BoundingBoxesJson"/>.</summary>
    [JsonIgnore]
    public int? CharStart { get; init; }

    [JsonIgnore]
    public int? CharEnd { get; init; }
}
