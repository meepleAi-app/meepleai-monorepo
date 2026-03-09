namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for MechanicDraft entity.
/// </summary>
public record MechanicDraftDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid CreatedBy,
    string GameTitle,
    string SummaryNotes,
    string MechanicsNotes,
    string VictoryNotes,
    string ResourcesNotes,
    string PhasesNotes,
    string QuestionsNotes,
    string SummaryDraft,
    string MechanicsDraft,
    string VictoryDraft,
    string ResourcesDraft,
    string PhasesDraft,
    string QuestionsDraft,
    DateTime CreatedAt,
    DateTime LastModified,
    string Status);

/// <summary>
/// Result of AI assist operation.
/// Contains the AI-generated draft text for a specific section.
/// </summary>
public record AiAssistResultDto(
    string Section,
    string GeneratedDraft);
