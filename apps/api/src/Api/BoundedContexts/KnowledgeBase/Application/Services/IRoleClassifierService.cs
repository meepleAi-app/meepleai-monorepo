using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Classifies text chunks from board game manuals into <see cref="GameBookRole"/> tags
/// (Tutorial, RulesReference, Narrative, Encounter, Lore, Setup). Multi-label allowed.
/// Heading-based heuristics first; LLM fallback for ambiguous chunks (D3).
/// </summary>
public interface IRoleClassifierService
{
    Task<IReadOnlyList<GameBookRole>> ClassifyAsync(
        IReadOnlyList<ChunkInput> chunks,
        CancellationToken cancellationToken);
}

/// <summary>
/// Input to role classification: heading breadcrumb (e.g. "Combat > Magic Combat")
/// and the chunk body text.
/// </summary>
public sealed record ChunkInput(string HeadingPath, string BodyText);
