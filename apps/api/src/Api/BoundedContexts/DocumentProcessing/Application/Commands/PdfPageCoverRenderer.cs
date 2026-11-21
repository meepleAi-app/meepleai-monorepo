using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #5100: render a PDF page, encode it as the cover preview and upload it under a key. Shared by
/// <see cref="MaterializePdfCoverCommandHandler"/>, which then makes it the PDF's cover, and
/// <see cref="RenderPdfPageCoverCommandHandler"/>, which leaves the PDF alone.
/// </summary>
internal static class PdfPageCoverRenderer
{
    /// <summary>
    /// Issue #3363: rendering a cover needs the R2 upload pipeline, registered only when STORAGE_PROVIDER=s3. Fail
    /// fast with a clear domain error in local-storage mode.
    /// </summary>
    public static IPdfCoverUploadPipeline RequireUploadPipeline(IPdfCoverUploadPipeline? uploadPipeline) =>
        uploadPipeline ?? throw new CoverMaterializationException(
            "Materializzazione cover PDF non disponibile: richiede storage S3/R2 (STORAGE_PROVIDER=s3).");

    /// <returns>The DB-stored key (no suffix) of the uploaded cover.</returns>
    public static async Task<string> RenderAndUploadAsync(
        IMediator mediator,
        IWebpVariantGenerator webpVariantGenerator,
        IPdfCoverUploadPipeline uploadPipeline,
        Guid pdfDocumentId,
        int pageNumber,
        string dbKey,
        CancellationToken cancellationToken)
    {
        byte[] jpeg;
        try
        {
            jpeg = await mediator
                .Send(new GetPdfPageImageQuery(pdfDocumentId, pageNumber), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // SmolDocling down/404: non-blocking failure, no "half-generated"
            // cover state — leave PdfDocument untouched.
            throw new CoverMaterializationException("Rendering pagina PDF non disponibile.", ex);
        }

        // The upload pipeline stores this under the {DbKey}-preview.webp physical key that
        // CoverUrlResolver serves as the L4 preview (600x900). Encode at preview dimensions
        // so a manually-materialized cover is not a 200x300 thumbnail stretched as a preview.
        var webpBytes = await webpVariantGenerator
            .GenerateWebpAsync(jpeg, PdfCoverExtractor.PreviewWidth, PdfCoverExtractor.PreviewHeight, cancellationToken)
            .ConfigureAwait(false);

        return await uploadPipeline.UploadAsync(dbKey, webpBytes, cancellationToken).ConfigureAwait(false);
    }
}
