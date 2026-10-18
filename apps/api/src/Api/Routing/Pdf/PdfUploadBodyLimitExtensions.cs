using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Routing;

/// <summary>
/// #4449: the request body limit of an endpoint that receives one PDF, derived from the single size
/// limit (<c>PdfProcessing:MaxFileSizeBytes</c>) instead of a number written next to each route.
/// </summary>
internal static class PdfUploadBodyLimitExtensions
{
    /// <summary>
    /// Sets the body limit to the PDF limit plus the multipart envelope
    /// (<see cref="PdfSizeLimit.RequestBodyLimit"/>). Without it Kestrel applies its default of
    /// 30 000 000 bytes: every PDF over ~28.6 MB gets a bare 413, whatever the endpoint's validator
    /// allows — which is what the private PDF upload, the wizards' upload and «Aggiungi RAG» did.
    /// </summary>
    public static RouteHandlerBuilder WithPdfUploadBodyLimit(this RouteHandlerBuilder builder, IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(routes);

        var maxFileSizeBytes = routes.ServiceProvider
            .GetRequiredService<IOptions<PdfProcessingOptions>>()
            .Value.MaxFileSizeBytes;

        return builder.WithMetadata(new RequestSizeLimitAttribute(PdfSizeLimit.RequestBodyLimit(maxFileSizeBytes)));
    }
}
