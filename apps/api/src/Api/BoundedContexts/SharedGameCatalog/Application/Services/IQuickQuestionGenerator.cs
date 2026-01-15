using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Service for generating quick questions from game rulebook content using AI.
/// </summary>
internal interface IQuickQuestionGenerator
{
    /// <summary>
    /// Generates quick questions for a game based on rulebook analysis.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="rulebookContent">The rulebook content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generation result containing questions and confidence score.</returns>
    Task<QuickQuestionGenerationResult> GenerateQuestionsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of quick question generation.
/// </summary>
/// <param name="Questions">The generated questions with their categories.</param>
/// <param name="ConfidenceScore">Overall confidence score (0-1).</param>
public record QuickQuestionGenerationResult(
    IReadOnlyCollection<GeneratedQuestion> Questions,
    decimal ConfidenceScore
);

/// <summary>
/// A single generated question with its category.
/// </summary>
/// <param name="Text">The question text.</param>
/// <param name="Category">The question category.</param>
/// <param name="Confidence">Confidence score for this specific question (0-1).</param>
public record GeneratedQuestion(
    string Text,
    QuestionCategory Category,
    decimal Confidence
);
