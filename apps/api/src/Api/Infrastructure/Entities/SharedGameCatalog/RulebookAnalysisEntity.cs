namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for RulebookAnalysis.
/// Stores structured information extracted from rulebook PDFs.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
public class RulebookAnalysisEntity
{
    /// <summary>
    /// Unique identifier for the analysis.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// FK to the shared game.
    /// </summary>
    public Guid SharedGameId { get; set; }

    /// <summary>
    /// FK to the PDF document that was analyzed.
    /// </summary>
    public Guid PdfDocumentId { get; set; }

    /// <summary>
    /// Extracted game title from rulebook.
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// High-level game summary.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Key game mechanics (stored as JSON array).
    /// </summary>
    public string KeyMechanicsJson { get; set; } = "[]";

    /// <summary>
    /// Victory conditions (stored as JSON object).
    /// </summary>
    public string? VictoryConditionsJson { get; set; }

    /// <summary>
    /// Game resources (stored as JSON array).
    /// </summary>
    public string ResourcesJson { get; set; } = "[]";

    /// <summary>
    /// Game phases (stored as JSON array).
    /// </summary>
    public string GamePhasesJson { get; set; } = "[]";

    /// <summary>
    /// Common questions (stored as JSON array).
    /// </summary>
    public string CommonQuestionsJson { get; set; } = "[]";

    /// <summary>
    /// Key concepts / glossary terms extracted from rulebook (stored as JSON array).
    /// Issue #5448: ExtractKeyConcepts glossary extraction.
    /// </summary>
    public string KeyConceptsJson { get; set; } = "[]";

    /// <summary>
    /// Generated FAQ entries from rulebook analysis (stored as JSON array).
    /// Issue #5449: GenerateQuestions FAQ generation.
    /// </summary>
    public string GeneratedFaqsJson { get; set; } = "[]";

    /// <summary>
    /// Game state tracking schema as JSON Schema string.
    /// Issue #5450: ExtractStateSchema game state tracking.
    /// </summary>
    public string? GameStateSchemaJson { get; set; }

    /// <summary>
    /// Analysis completion status (0=Complete, 1=PartiallyComplete, 2=Failed).
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public int CompletionStatus { get; set; }

    /// <summary>
    /// Missing critical sections as JSON array (when PartiallyComplete).
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public string MissingSectionsJson { get; set; } = "[]";

    /// <summary>
    /// AI confidence score (0-1).
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0", "2.1").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Whether this is the active version.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Source of generation (0=AI, 1=Manual).
    /// </summary>
    public int Source { get; set; }

    /// <summary>
    /// When the analysis was performed.
    /// </summary>
    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// User who triggered this analysis.
    /// </summary>
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public SharedGameEntity SharedGame { get; set; } = default!;
}
