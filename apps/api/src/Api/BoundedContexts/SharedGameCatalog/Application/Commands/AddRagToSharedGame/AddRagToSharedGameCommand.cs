using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

/// <summary>
/// Orchestration command that uploads a PDF, links it to a SharedGame,
/// optionally auto-approves for RAG processing (admin), and enqueues for indexing.
/// Editors require separate approval before processing begins.
/// </summary>
internal record AddRagToSharedGameCommand(
    Guid SharedGameId,
    IFormFile File,
    SharedGameDocumentType DocumentType,
    string Version,
    List<string>? Tags,
    Guid UserId,
    bool IsAdmin
) : ICommand<AddRagToSharedGameResult>;

/// <summary>
/// Result of the RAG wizard saga.
/// ProcessingJobId is null when: editor upload (pending approval) or enqueue failed (graceful degradation).
/// </summary>
internal record AddRagToSharedGameResult(
    Guid PdfDocumentId,
    Guid SharedGameDocumentId,
    Guid? ProcessingJobId,
    bool AutoApproved,
    string StreamUrl
);
