namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Uploads a materialized PDF cover WebP to R2. See
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver.ResolvePublicAsync"/>
/// for the L4 read-side convention this pipeline's write side must match:
/// the DB-stored key (<c>dbKey</c>, no suffix) resolves to the physical R2
/// object <c>{dbKey}-preview.webp</c>.
/// </summary>
internal interface IPdfCoverUploadPipeline
{
    /// <summary>
    /// Uploads <paramref name="webpBytes"/> to R2 as <c>{dbKey}-preview.webp</c>
    /// with <c>Content-Type: image/webp</c> and an immutable cache-control
    /// header, then returns <paramref name="dbKey"/> unchanged (no suffix) —
    /// the value to persist on <c>PdfDocument.CoverR2Key</c>.
    /// </summary>
    Task<string> UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken);
}
