using Api.Models;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to generate AI-powered follow-up questions based on Q&amp;A context.
/// CHAT-02: AI-Generated Follow-Up Questions
/// </summary>
internal sealed record GenerateFollowUpQuestionsQuery : IRequest<IReadOnlyList<string>>
{
    /// <summary>
    /// The user's original question.
    /// </summary>
    public required string OriginalQuestion { get; init; }

    /// <summary>
    /// The AI-generated answer.
    /// </summary>
    public required string GeneratedAnswer { get; init; }

    /// <summary>
    /// RAG context snippets used to generate the answer.
    /// </summary>
    public required IReadOnlyList<Snippet> RagContext { get; init; }

    /// <summary>
    /// Name of the game for context-specific questions.
    /// </summary>
    public required string GameName { get; init; }

    /// <summary>
    /// Maximum number of questions to generate (default: 5, limited by configuration).
    /// </summary>
    public int MaxQuestions { get; init; } = 5;
}
