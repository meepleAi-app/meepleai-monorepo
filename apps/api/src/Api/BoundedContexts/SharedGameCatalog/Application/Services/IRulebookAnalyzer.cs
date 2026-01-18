using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Service for analyzing rulebook PDFs to extract structured game information.
/// </summary>
internal interface IRulebookAnalyzer
{
    /// <summary>
    /// Performs a comprehensive analysis of a rulebook to extract all structured information.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="rulebookContent">The rulebook text content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete analysis result with game information and confidence score.</returns>
    Task<RulebookAnalysisResult> AnalyzeAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts game state schema from rulebook content.
    /// Used for GameStateTemplate generation.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="rulebookContent">The rulebook text content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted game state schema structure.</returns>
    Task<GameStateSchemaResult> ExtractStateSchemaAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates quick questions from rulebook content.
    /// Used for QuickQuestion generation.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="rulebookContent">The rulebook text content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated questions with confidence scores.</returns>
    Task<RulebookQuestionsResult> GenerateQuestionsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts key concepts and terminology from rulebook content.
    /// </summary>
    /// <param name="gameName">The name of the game.</param>
    /// <param name="rulebookContent">The rulebook text content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of key concepts with definitions.</returns>
    Task<KeyConceptsResult> ExtractKeyConceptsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of comprehensive rulebook analysis.
/// </summary>
/// <param name="GameTitle">Extracted game title.</param>
/// <param name="Summary">High-level game summary.</param>
/// <param name="KeyMechanics">List of key game mechanics.</param>
/// <param name="VictoryConditions">Victory conditions for the game.</param>
/// <param name="Resources">Game resources (wood, gold, etc.).</param>
/// <param name="GamePhases">Game phases in turn order.</param>
/// <param name="CommonQuestions">Common player questions.</param>
/// <param name="ConfidenceScore">Overall confidence score (0-1).</param>
public record RulebookAnalysisResult(
    string GameTitle,
    string Summary,
    List<string> KeyMechanics,
    VictoryConditions? VictoryConditions,
    List<Resource> Resources,
    List<GamePhase> GamePhases,
    List<string> CommonQuestions,
    decimal ConfidenceScore
);

// Note: GameStateSchemaResult is defined in IGameStateSchemaGenerator.cs

/// <summary>
/// Result of question generation from rulebook.
/// </summary>
/// <param name="Questions">Generated questions with categories.</param>
/// <param name="ConfidenceScore">Overall confidence score (0-1).</param>
public record RulebookQuestionsResult(
    IReadOnlyCollection<GeneratedRulebookQuestion> Questions,
    decimal ConfidenceScore
);

/// <summary>
/// A generated question from rulebook analysis.
/// </summary>
/// <param name="Text">The question text.</param>
/// <param name="Answer">The answer extracted from rulebook.</param>
/// <param name="PageReferences">Page numbers where answer was found.</param>
/// <param name="Confidence">Confidence score for this question (0-1).</param>
public record GeneratedRulebookQuestion(
    string Text,
    string Answer,
    List<int> PageReferences,
    decimal Confidence
);

/// <summary>
/// Result of key concept extraction.
/// </summary>
/// <param name="Concepts">Key concepts with definitions.</param>
/// <param name="ConfidenceScore">Overall confidence score (0-1).</param>
public record KeyConceptsResult(
    IReadOnlyCollection<KeyConcept> Concepts,
    decimal ConfidenceScore
);

/// <summary>
/// A key concept extracted from the rulebook.
/// </summary>
/// <param name="Term">The concept term or name.</param>
/// <param name="Definition">The concept definition.</param>
/// <param name="Category">Category (e.g., "Mechanic", "Component", "Rule").</param>
public record KeyConcept(
    string Term,
    string Definition,
    string Category
);
