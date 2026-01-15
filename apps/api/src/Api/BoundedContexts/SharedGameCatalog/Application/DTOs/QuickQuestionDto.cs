using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Data transfer object for a quick question.
/// </summary>
/// <param name="Id">The unique identifier of the question.</param>
/// <param name="SharedGameId">The ID of the game this question belongs to.</param>
/// <param name="Text">The question text.</param>
/// <param name="Emoji">The emoji icon for the question.</param>
/// <param name="Category">The category of the question.</param>
/// <param name="DisplayOrder">The display order (0-based).</param>
/// <param name="IsGenerated">Whether this question was AI-generated.</param>
/// <param name="CreatedAt">When the question was created.</param>
/// <param name="IsActive">Whether the question is active.</param>
public record QuickQuestionDto(
    Guid Id,
    Guid SharedGameId,
    string Text,
    string Emoji,
    QuestionCategory Category,
    int DisplayOrder,
    bool IsGenerated,
    DateTime CreatedAt,
    bool IsActive
);

/// <summary>
/// Response DTO for AI generation of quick questions.
/// </summary>
/// <param name="Questions">The generated questions.</param>
/// <param name="ConfidenceScore">Overall confidence score of the generation (0-1).</param>
/// <param name="GeneratedAt">When the generation occurred.</param>
public record GenerateQuickQuestionsResultDto(
    IReadOnlyCollection<QuickQuestionDto> Questions,
    decimal ConfidenceScore,
    DateTime GeneratedAt
);
