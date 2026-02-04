using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve a document for RAG processing.
/// </summary>
/// <param name="DocumentId">The ID of the document to approve</param>
/// <param name="ApprovedBy">The ID of the admin approving the document</param>
/// <param name="Notes">Optional notes from the approver</param>
internal record ApproveDocumentForRagProcessingCommand(
    Guid DocumentId,
    Guid ApprovedBy,
    string? Notes = null) : ICommand<Unit>;
