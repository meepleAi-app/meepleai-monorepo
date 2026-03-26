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
    bool IsPublic = false);
