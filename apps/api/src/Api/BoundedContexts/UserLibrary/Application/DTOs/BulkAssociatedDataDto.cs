namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO containing aggregated counts of data associated with multiple collection entries.
/// Used for bulk removal warnings.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
/// <param name="TotalCustomAgents">Total number of entries with custom AI agent configurations.</param>
/// <param name="TotalPrivatePdfs">Total number of entries with private PDFs uploaded.</param>
/// <param name="TotalChatSessions">Total number of chat sessions across all entries.</param>
/// <param name="TotalGameSessions">Total number of recorded game sessions across all entries.</param>
/// <param name="TotalChecklistItems">Total number of setup checklist items across all entries.</param>
/// <param name="TotalLabels">Total number of labels assigned across all entries.</param>
internal record BulkAssociatedDataDto(
    int TotalCustomAgents,
    int TotalPrivatePdfs,
    int TotalChatSessions,
    int TotalGameSessions,
    int TotalChecklistItems,
    int TotalLabels
);
