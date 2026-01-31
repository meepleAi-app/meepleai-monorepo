using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to index a PDF document for semantic search.
/// Orchestrates: text extraction → chunking → embedding → Qdrant indexing
/// </summary>
/// <param name="PdfId">PDF document identifier</param>
internal record IndexPdfCommand(string PdfId) : ICommand<IndexingResultDto>;
