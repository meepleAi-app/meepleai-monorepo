using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to accept the copyright disclaimer for a PDF document.
/// Issue #5446: Required before processing starts.
/// </summary>
internal sealed record AcceptCopyrightDisclaimerCommand(Guid UserId, Guid PdfDocumentId) : ICommand<AcceptCopyrightDisclaimerResult>;

/// <summary>
/// Result of accepting the copyright disclaimer.
/// </summary>
internal sealed record AcceptCopyrightDisclaimerResult(bool Success, string Message, Guid? PdfDocumentId);
