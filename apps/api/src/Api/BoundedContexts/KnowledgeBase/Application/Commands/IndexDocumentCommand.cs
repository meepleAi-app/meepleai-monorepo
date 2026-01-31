using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to index a PDF document into the vector database.
/// Triggers embedding generation and Qdrant indexing.
/// </summary>
internal record IndexDocumentCommand(
    Guid PdfDocumentId,
    Guid GameId,
    string Language = "en"
) : ICommand<Guid>; // Returns VectorDocument ID
