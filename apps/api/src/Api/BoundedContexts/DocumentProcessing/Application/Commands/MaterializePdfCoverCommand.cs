using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Renders <see cref="PageNumber"/> of the given PDF, encodes it as a WebP
/// cover thumbnail, uploads it to R2 under <see cref="DbKey"/>, and marks
/// the <c>PdfDocument</c>'s cover as generated. Materialization is
/// synchronous. Returns the DB-stored key (no suffix) — see
/// <see cref="Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfCoverUploadPipeline"/>
/// for the R2 key convention.
/// </summary>
internal sealed record MaterializePdfCoverCommand(Guid PdfDocumentId, int PageNumber, string DbKey) : ICommand<string>;

/// <summary>
/// Thrown when the PDF page render step fails (e.g. the SmolDocling page-image
/// service is unavailable or returns 404). This is a non-blocking failure:
/// the caller should not retry indefinitely, and the PDF's cover state is
/// left untouched (no "half-generated" state).
/// </summary>
public sealed class CoverMaterializationException : Exception
{
    public CoverMaterializationException(string message, Exception inner) : base(message, inner)
    {
    }

    public CoverMaterializationException()
    {
    }

    public CoverMaterializationException(string message) : base(message)
    {
    }
}
