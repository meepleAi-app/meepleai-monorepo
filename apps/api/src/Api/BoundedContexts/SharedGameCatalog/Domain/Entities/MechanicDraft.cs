using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Work-in-progress entity for the Mechanic Extractor (Variant C workflow).
/// Stores human notes and AI-generated drafts per section before finalization
/// into a RulebookAnalysis entry.
/// </summary>
public sealed class MechanicDraft : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private Guid _pdfDocumentId;
    private Guid _createdBy;
    private readonly string _gameTitle = string.Empty;

    // Per-section human notes (what the human writes while reading the PDF)
    private string _summaryNotes = string.Empty;
    private string _mechanicsNotes = string.Empty;
    private string _victoryNotes = string.Empty;
    private string _resourcesNotes = string.Empty;
    private string _phasesNotes = string.Empty;
    private string _questionsNotes = string.Empty;

    // Per-section AI drafts (accepted AI-generated text from notes)
    private string _summaryDraft = string.Empty;
    private string _mechanicsDraft = string.Empty;   // JSON array of strings
    private string _victoryDraft = string.Empty;      // JSON object (VictoryConditions)
    private string _resourcesDraft = string.Empty;    // JSON array of Resource
    private string _phasesDraft = string.Empty;       // JSON array of GamePhase
    private string _questionsDraft = string.Empty;    // JSON array of strings

    private readonly DateTime _createdAt;
    private DateTime _lastModified;
    private MechanicDraftStatus _status;

    public new Guid Id => _id;
    public Guid SharedGameId => _sharedGameId;
    public Guid PdfDocumentId => _pdfDocumentId;
    public Guid CreatedBy => _createdBy;
    public string GameTitle => _gameTitle;

    public string SummaryNotes => _summaryNotes;
    public string MechanicsNotes => _mechanicsNotes;
    public string VictoryNotes => _victoryNotes;
    public string ResourcesNotes => _resourcesNotes;
    public string PhasesNotes => _phasesNotes;
    public string QuestionsNotes => _questionsNotes;

    public string SummaryDraft => _summaryDraft;
    public string MechanicsDraft => _mechanicsDraft;
    public string VictoryDraft => _victoryDraft;
    public string ResourcesDraft => _resourcesDraft;
    public string PhasesDraft => _phasesDraft;
    public string QuestionsDraft => _questionsDraft;

    public DateTime CreatedAt => _createdAt;
    public DateTime LastModified => _lastModified;
    public MechanicDraftStatus Status => _status;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private MechanicDraft() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal MechanicDraft(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        Guid createdBy,
        string gameTitle,
        string summaryNotes,
        string mechanicsNotes,
        string victoryNotes,
        string resourcesNotes,
        string phasesNotes,
        string questionsNotes,
        string summaryDraft,
        string mechanicsDraft,
        string victoryDraft,
        string resourcesDraft,
        string phasesDraft,
        string questionsDraft,
        DateTime createdAt,
        DateTime lastModified,
        MechanicDraftStatus status) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _pdfDocumentId = pdfDocumentId;
        _createdBy = createdBy;
        _gameTitle = gameTitle;
        _summaryNotes = summaryNotes;
        _mechanicsNotes = mechanicsNotes;
        _victoryNotes = victoryNotes;
        _resourcesNotes = resourcesNotes;
        _phasesNotes = phasesNotes;
        _questionsNotes = questionsNotes;
        _summaryDraft = summaryDraft;
        _mechanicsDraft = mechanicsDraft;
        _victoryDraft = victoryDraft;
        _resourcesDraft = resourcesDraft;
        _phasesDraft = phasesDraft;
        _questionsDraft = questionsDraft;
        _createdAt = createdAt;
        _lastModified = lastModified;
        _status = status;
    }

    /// <summary>
    /// Creates a new mechanic extraction draft for a game.
    /// </summary>
    public static MechanicDraft Create(
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

        var now = DateTime.UtcNow;

        return new MechanicDraft(
            Guid.NewGuid(),
            sharedGameId,
            pdfDocumentId,
            createdBy,
            gameTitle.Trim(),
            summaryNotes: string.Empty,
            mechanicsNotes: string.Empty,
            victoryNotes: string.Empty,
            resourcesNotes: string.Empty,
            phasesNotes: string.Empty,
            questionsNotes: string.Empty,
            summaryDraft: string.Empty,
            mechanicsDraft: string.Empty,
            victoryDraft: string.Empty,
            resourcesDraft: string.Empty,
            phasesDraft: string.Empty,
            questionsDraft: string.Empty,
            createdAt: now,
            lastModified: now,
            MechanicDraftStatus.Draft);
    }

    /// <summary>
    /// Updates human notes for a specific section.
    /// </summary>
    public void UpdateNotes(
        string section,
        string notes)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new ArgumentException("Section cannot be empty", nameof(section));

        if (_status == MechanicDraftStatus.Activated)
            throw new InvalidOperationException("Cannot modify an activated draft");

        switch (section.ToLowerInvariant())
        {
            case "summary": _summaryNotes = notes ?? string.Empty; break;
            case "mechanics": _mechanicsNotes = notes ?? string.Empty; break;
            case "victory": _victoryNotes = notes ?? string.Empty; break;
            case "resources": _resourcesNotes = notes ?? string.Empty; break;
            case "phases": _phasesNotes = notes ?? string.Empty; break;
            case "questions": _questionsNotes = notes ?? string.Empty; break;
            default:
                throw new ArgumentException($"Unknown section: {section}", nameof(section));
        }

        _lastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the AI-generated draft for a specific section.
    /// Called when the human accepts an AI assist result.
    /// </summary>
    public void AcceptDraft(
        string section,
        string draft)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new ArgumentException("Section cannot be empty", nameof(section));

        if (_status == MechanicDraftStatus.Activated)
            throw new InvalidOperationException("Cannot modify an activated draft");

        switch (section.ToLowerInvariant())
        {
            case "summary": _summaryDraft = draft ?? string.Empty; break;
            case "mechanics": _mechanicsDraft = draft ?? string.Empty; break;
            case "victory": _victoryDraft = draft ?? string.Empty; break;
            case "resources": _resourcesDraft = draft ?? string.Empty; break;
            case "phases": _phasesDraft = draft ?? string.Empty; break;
            case "questions": _questionsDraft = draft ?? string.Empty; break;
            default:
                throw new ArgumentException($"Unknown section: {section}", nameof(section));
        }

        _lastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the draft as completed (all sections reviewed).
    /// </summary>
    public void MarkCompleted()
    {
        if (_status == MechanicDraftStatus.Activated)
            throw new InvalidOperationException("Cannot modify an activated draft");

        _status = MechanicDraftStatus.Completed;
        _lastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the draft as activated (finalized into RulebookAnalysis).
    /// </summary>
    public void MarkActivated()
    {
        _status = MechanicDraftStatus.Activated;
        _lastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Bulk updates all notes at once (auto-save scenario).
    /// </summary>
    public void UpdateAllNotes(
        string summaryNotes,
        string mechanicsNotes,
        string victoryNotes,
        string resourcesNotes,
        string phasesNotes,
        string questionsNotes)
    {
        if (_status == MechanicDraftStatus.Activated)
            throw new InvalidOperationException("Cannot modify an activated draft");

        _summaryNotes = summaryNotes ?? string.Empty;
        _mechanicsNotes = mechanicsNotes ?? string.Empty;
        _victoryNotes = victoryNotes ?? string.Empty;
        _resourcesNotes = resourcesNotes ?? string.Empty;
        _phasesNotes = phasesNotes ?? string.Empty;
        _questionsNotes = questionsNotes ?? string.Empty;
        _lastModified = DateTime.UtcNow;
    }
}
