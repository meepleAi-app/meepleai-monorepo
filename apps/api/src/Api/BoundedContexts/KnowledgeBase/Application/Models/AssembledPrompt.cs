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
/// </summary>
internal sealed record ChunkCitation(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview);
