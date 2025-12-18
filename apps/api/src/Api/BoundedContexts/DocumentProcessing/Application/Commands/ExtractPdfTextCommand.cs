using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to trigger text extraction for an existing PDF document.
/// Used to reprocess PDFs that are stuck in pending status or need re-extraction.
/// </summary>
internal record ExtractPdfTextCommand(Guid PdfId) : ICommand<ExtractPdfTextResultDto>;
