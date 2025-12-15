using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to set the public visibility of a PDF document.
/// When IsPublic is true, the PDF is visible in the public library to all registered users.
/// </summary>
internal record SetPdfVisibilityCommand(Guid PdfId, bool IsPublic) : ICommand<SetPdfVisibilityResult>;

/// <summary>
/// Result of setting PDF visibility.
/// </summary>
internal record SetPdfVisibilityResult(bool Success, string Message, Guid? PdfId);
