namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for MechanicDraft.
/// Stores work-in-progress mechanic extraction data (Variant C workflow).
/// </summary>
public class MechanicDraftEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public Guid PdfDocumentId { get; set; }
    public Guid CreatedBy { get; set; }
    public string GameTitle { get; set; } = string.Empty;

    // Human notes per section
    public string SummaryNotes { get; set; } = string.Empty;
    public string MechanicsNotes { get; set; } = string.Empty;
    public string VictoryNotes { get; set; } = string.Empty;
    public string ResourcesNotes { get; set; } = string.Empty;
    public string PhasesNotes { get; set; } = string.Empty;
    public string QuestionsNotes { get; set; } = string.Empty;

    // AI-generated drafts per section
    public string SummaryDraft { get; set; } = string.Empty;
    public string MechanicsDraft { get; set; } = string.Empty;
    public string VictoryDraft { get; set; } = string.Empty;
    public string ResourcesDraft { get; set; } = string.Empty;
    public string PhasesDraft { get; set; } = string.Empty;
    public string QuestionsDraft { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Status: 0=Draft, 1=Completed, 2=Activated.
    /// </summary>
    public int Status { get; set; }

    // Navigation properties
    public SharedGameEntity SharedGame { get; set; } = default!;
}
