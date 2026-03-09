using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to toggle the IsActiveForRag flag on a PDF document.
/// Issue #5446: Deactivated docs remain in Qdrant but excluded from search.
/// </summary>
internal record SetActiveForRagCommand(Guid PdfId, bool IsActive) : ICommand<SetActiveForRagResult>;

/// <summary>
/// Result of setting the RAG active flag.
/// </summary>
internal record SetActiveForRagResult(bool Success, string Message, Guid? PdfId);
