using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// A key concept extracted from a rulebook (e.g., game terms, mechanics, components).
/// Issue #5448: ExtractKeyConcepts glossary extraction.
/// </summary>
public record KeyConcept(string Term, string Definition, string Category);

/// <summary>
/// A generated FAQ entry from rulebook analysis.
/// Issue #5449: GenerateQuestions FAQ generation.
/// </summary>
public record GeneratedFaq(
    string Question,
    string Answer,
    string SourceSection,
    decimal Confidence,
    List<string> Tags);

/// <summary>
/// Entity representing a structured analysis of a rulebook PDF.
/// Contains extracted game information like mechanics, victory conditions, resources, and phases.
/// Supports multi-versioning with one active analysis per game.
/// </summary>
public sealed class RulebookAnalysis : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private Guid _pdfDocumentId;
    private readonly string _gameTitle = string.Empty;
    private string _summary = string.Empty;
    private List<string> _keyMechanics = new();
    private VictoryConditions? _victoryConditions;
    private List<Resource> _resources = new();
    private List<GamePhase> _gamePhases = new();
    private List<string> _commonQuestions = new();
    private List<KeyConcept> _keyConcepts = new();
    private List<GeneratedFaq> _generatedFaqs = new();
    private string? _gameStateSchemaJson;
    private AnalysisCompletionStatus _completionStatus = AnalysisCompletionStatus.Complete;
    private List<string> _missingSections = new();
    private decimal _confidenceScore;
    private readonly string _version = string.Empty;
    private bool _isActive;
    private GenerationSource _source;
    private readonly DateTime _analyzedAt;
    private Guid _createdBy;

    /// <summary>
    /// Gets the unique identifier of this analysis.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the shared game this analysis belongs to.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets the ID of the PDF document that was analyzed.
    /// </summary>
    public Guid PdfDocumentId => _pdfDocumentId;

    /// <summary>
    /// Gets the extracted game title from the rulebook.
    /// </summary>
    public string GameTitle => _gameTitle;

    /// <summary>
    /// Gets the high-level summary of the game.
    /// </summary>
    public string Summary => _summary;

    /// <summary>
    /// Gets the list of key game mechanics (e.g., "Worker Placement", "Deck Building").
    /// </summary>
    public IReadOnlyList<string> KeyMechanics => _keyMechanics.AsReadOnly();

    /// <summary>
    /// Gets the victory conditions for the game.
    /// </summary>
    public VictoryConditions? VictoryConditions => _victoryConditions;

    /// <summary>
    /// Gets the list of game resources (e.g., wood, gold, cards).
    /// </summary>
    public IReadOnlyList<Resource> Resources => _resources.AsReadOnly();

    /// <summary>
    /// Gets the list of game phases in turn order.
    /// </summary>
    public IReadOnlyList<GamePhase> GamePhases => _gamePhases.AsReadOnly();

    /// <summary>
    /// Gets the list of common questions players might have about the game.
    /// </summary>
    public IReadOnlyList<string> CommonQuestions => _commonQuestions.AsReadOnly();

    /// <summary>
    /// Gets the list of key concepts extracted from the rulebook (glossary terms).
    /// Issue #5448: ExtractKeyConcepts glossary extraction.
    /// </summary>
    public IReadOnlyList<KeyConcept> KeyConcepts => _keyConcepts.AsReadOnly();

    /// <summary>
    /// Gets the list of generated FAQ entries from rulebook analysis.
    /// Issue #5449: GenerateQuestions FAQ generation.
    /// </summary>
    public IReadOnlyList<GeneratedFaq> GeneratedFaqs => _generatedFaqs.AsReadOnly();

    /// <summary>
    /// Gets the game state schema as a JSON Schema string.
    /// Used by frontend for game state display during live sessions.
    /// Issue #5450: ExtractStateSchema game state tracking.
    /// </summary>
    public string? GameStateSchemaJson => _gameStateSchemaJson;

    /// <summary>
    /// Gets the analysis completion status based on critical section coverage.
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public AnalysisCompletionStatus CompletionStatus => _completionStatus;

    /// <summary>
    /// Gets the list of missing critical sections (if PartiallyComplete).
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public IReadOnlyList<string> MissingSections => _missingSections.AsReadOnly();

    /// <summary>
    /// Gets the AI confidence score (0-1) for this analysis.
    /// </summary>
    public decimal ConfidenceScore => _confidenceScore;

    /// <summary>
    /// Gets the version string (e.g., "1.0", "2.1").
    /// </summary>
    public string Version => _version;

    /// <summary>
    /// Gets whether this is the active version for the game.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the source of this analysis (AI or Manual).
    /// </summary>
    public GenerationSource Source => _source;

    /// <summary>
    /// Gets the timestamp when this analysis was performed.
    /// </summary>
    public DateTime AnalyzedAt => _analyzedAt;

    /// <summary>
    /// Gets the ID of the user who triggered this analysis.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private RulebookAnalysis() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal RulebookAnalysis(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameTitle,
        string summary,
        List<string> keyMechanics,
        VictoryConditions? victoryConditions,
        List<Resource> resources,
        List<GamePhase> gamePhases,
        List<string> commonQuestions,
        decimal confidenceScore,
        string version,
        bool isActive,
        GenerationSource source,
        DateTime analyzedAt,
        Guid createdBy,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string? gameStateSchemaJson = null,
        AnalysisCompletionStatus completionStatus = AnalysisCompletionStatus.Complete,
        List<string>? missingSections = null) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _pdfDocumentId = pdfDocumentId;
        _gameTitle = gameTitle;
        _summary = summary;
        _keyMechanics = keyMechanics;
        _victoryConditions = victoryConditions;
        _resources = resources;
        _gamePhases = gamePhases;
        _commonQuestions = commonQuestions;
        _keyConcepts = keyConcepts ?? new List<KeyConcept>();
        _generatedFaqs = generatedFaqs ?? new List<GeneratedFaq>();
        _gameStateSchemaJson = gameStateSchemaJson;
        _completionStatus = completionStatus;
        _missingSections = missingSections ?? new List<string>();
        _confidenceScore = confidenceScore;
        _version = version;
        _isActive = isActive;
        _source = source;
        _analyzedAt = analyzedAt;
        _createdBy = createdBy;
    }

    /// <summary>
    /// Creates a new AI-generated RulebookAnalysis.
    /// </summary>
    public static RulebookAnalysis CreateFromAI(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameTitle,
        string summary,
        List<string> keyMechanics,
        VictoryConditions? victoryConditions,
        List<Resource> resources,
        List<GamePhase> gamePhases,
        List<string> commonQuestions,
        decimal confidenceScore,
        Guid createdBy,
        string? version = null,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string? gameStateSchemaJson = null)
    {
        ValidateCreateParameters(sharedGameId, pdfDocumentId, gameTitle, createdBy);

        if (confidenceScore < 0 || confidenceScore > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1");

        var analysisVersion = version != null
            ? DocumentVersion.Create(version)
            : DocumentVersion.Default;

        return new RulebookAnalysis(
            Guid.NewGuid(),
            sharedGameId,
            pdfDocumentId,
            gameTitle.Trim(),
            summary.Trim(),
            keyMechanics ?? new List<string>(),
            victoryConditions,
            resources ?? new List<Resource>(),
            gamePhases ?? new List<GamePhase>(),
            commonQuestions ?? new List<string>(),
            confidenceScore,
            analysisVersion.Value,
            false, // Not active by default
            GenerationSource.AI,
            DateTime.UtcNow,
            createdBy,
            keyConcepts,
            generatedFaqs,
            gameStateSchemaJson);
    }

    /// <summary>
    /// Creates a new manually created RulebookAnalysis.
    /// </summary>
    public static RulebookAnalysis CreateManual(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameTitle,
        string summary,
        List<string> keyMechanics,
        VictoryConditions? victoryConditions,
        List<Resource> resources,
        List<GamePhase> gamePhases,
        List<string> commonQuestions,
        Guid createdBy,
        string? version = null,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string? gameStateSchemaJson = null)
    {
        ValidateCreateParameters(sharedGameId, pdfDocumentId, gameTitle, createdBy);

        var analysisVersion = version != null
            ? DocumentVersion.Create(version)
            : DocumentVersion.Default;

        return new RulebookAnalysis(
            Guid.NewGuid(),
            sharedGameId,
            pdfDocumentId,
            gameTitle.Trim(),
            summary.Trim(),
            keyMechanics ?? new List<string>(),
            victoryConditions,
            resources ?? new List<Resource>(),
            gamePhases ?? new List<GamePhase>(),
            commonQuestions ?? new List<string>(),
            0.0m, // No confidence score for manual
            analysisVersion.Value,
            false, // Not active by default
            GenerationSource.Manual,
            DateTime.UtcNow,
            createdBy,
            keyConcepts,
            generatedFaqs,
            gameStateSchemaJson);
    }

    /// <summary>
    /// Marks this analysis as partially complete with missing critical sections.
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public void MarkAsPartiallyComplete(List<string> missingSections)
    {
        _completionStatus = AnalysisCompletionStatus.PartiallyComplete;
        _missingSections = missingSections ?? new List<string>();
    }

    /// <summary>
    /// Sets this analysis as the active version.
    /// </summary>
    public void SetAsActive()
    {
        _isActive = true;
    }

    /// <summary>
    /// Deactivates this analysis version.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Updates the analysis content with new data.
    /// </summary>
    public void UpdateContent(
        string summary,
        List<string> keyMechanics,
        VictoryConditions? victoryConditions,
        List<Resource> resources,
        List<GamePhase> gamePhases,
        List<string> commonQuestions,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string? gameStateSchemaJson = null)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary cannot be empty", nameof(summary));

        _summary = summary.Trim();
        _keyMechanics = keyMechanics ?? new List<string>();
        _victoryConditions = victoryConditions;
        _resources = resources ?? new List<Resource>();
        _gamePhases = gamePhases ?? new List<GamePhase>();
        _commonQuestions = commonQuestions ?? new List<string>();
        _keyConcepts = keyConcepts ?? new List<KeyConcept>();
        _generatedFaqs = generatedFaqs ?? new List<GeneratedFaq>();
        _gameStateSchemaJson = gameStateSchemaJson;
        _source = GenerationSource.Manual; // Mark as manually edited
        _confidenceScore = 0.0m; // Clear confidence after manual edit
    }

    private static void ValidateCreateParameters(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameTitle,
        Guid createdBy)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PdfDocumentId cannot be empty", nameof(pdfDocumentId));

        if (string.IsNullOrWhiteSpace(gameTitle))
            throw new ArgumentException("Game title cannot be empty", nameof(gameTitle));

        if (gameTitle.Length > 300)
            throw new ArgumentException("Game title cannot exceed 300 characters", nameof(gameTitle));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));
    }
}
