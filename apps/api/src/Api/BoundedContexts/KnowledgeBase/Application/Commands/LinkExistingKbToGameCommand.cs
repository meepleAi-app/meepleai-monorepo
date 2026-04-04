using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Links an existing KB (VectorDocument) to a different game by cloning the VectorDocument metadata.
/// The underlying pgvector embeddings are shared (same PdfDocumentId).
/// </summary>
internal record LinkExistingKbToGameCommand(
    Guid UserId,
    Guid TargetGameId,
    Guid SourcePdfDocumentId
) : ICommand<LinkKbResultDto>;
