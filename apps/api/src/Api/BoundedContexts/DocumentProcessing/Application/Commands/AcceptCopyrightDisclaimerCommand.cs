using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to accept the copyright disclaimer for a PDF document.
/// Issue #5446: Required before processing starts.
/// </summary>
internal record AcceptCopyrightDisclaimerCommand(Guid PdfId, Guid UserId) : ICommand<AcceptCopyrightDisclaimerResult>;

/// <summary>
/// Result of accepting the copyright disclaimer.
/// </summary>
internal record AcceptCopyrightDisclaimerResult(bool Success, string Message, Guid? PdfId);
