using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to delete a PDF document and its associated vectors.
/// </summary>
public record DeletePdfCommand(string PdfId) : ICommand<PdfDeleteResult>;
