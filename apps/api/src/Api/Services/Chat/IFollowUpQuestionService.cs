using Api.Models;

namespace Api.Services.Chat;

/// <summary>
/// Service for generating AI-powered follow-up questions based on Q&A context.
/// CHAT-02: AI-Generated Follow-Up Questions
/// </summary>
public interface IFollowUpQuestionService
{
    /// <summary>
    /// Generates follow-up questions based on the original question, answer, and RAG context.
    /// </summary>
    /// <param name="originalQuestion">The user's original question</param>
    /// <param name="generatedAnswer">The AI-generated answer</param>
    /// <param name="ragContext">RAG context snippets used to generate the answer</param>
    /// <param name="gameName">Name of the game for context-specific questions</param>
    /// <param name="maxQuestions">Maximum number of questions to generate (default: 5)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of follow-up questions (empty list if generation fails)</returns>
    Task<IReadOnlyList<string>> GenerateQuestionsAsync(
        string originalQuestion,
        string generatedAnswer,
        IReadOnlyList<Snippet> ragContext,
        string gameName,
        int maxQuestions = 5,
        CancellationToken ct = default);
}
