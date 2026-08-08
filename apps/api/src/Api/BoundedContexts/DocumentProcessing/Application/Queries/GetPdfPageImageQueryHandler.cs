using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetPdfPageImageQuery.
/// Retrieves the PDF from blob storage and calls SmolDocling /api/v1/page-image
/// to extract a single page as a JPEG image for use in the wizard cover image picker.
/// </summary>
internal sealed class GetPdfPageImageQueryHandler : IQueryHandler<GetPdfPageImageQuery, byte[]>
{
    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GetPdfPageImageQueryHandler> _logger;

    public GetPdfPageImageQueryHandler(
        IPdfDocumentRepository pdfRepo,
        IBlobStorageService blobStorageService,
        IHttpClientFactory httpClientFactory,
        ILogger<GetPdfPageImageQueryHandler> logger)
    {
        _pdfRepo = pdfRepo ?? throw new ArgumentNullException(nameof(pdfRepo));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> Handle(GetPdfPageImageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(query), "Page number must be >= 1");

        var pdfDoc = await _pdfRepo.GetByIdAsync(query.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
            throw new NotFoundException($"PdfDocument {query.PdfDocumentId} not found");

        // Resolve storage bucket
        // Task 4: bucket key decoupled from gameId — uses pdf.Id (see PdfStorageKey + rebucket scripts)
        var bucket = PdfStorageKey.ForPdf(pdfDoc.Id);

        // Extract file ID from stored path
        var fileId = ExtractFileIdFromPath(pdfDoc.FilePath);
        if (string.IsNullOrEmpty(fileId))
            throw new InvalidOperationException($"Cannot extract fileId from path: {pdfDoc.FilePath}");

        // Retrieve PDF bytes from blob storage
        var pdfStream = await _blobStorageService.RetrieveAsync(fileId, BlobCategory.Pdf, bucket, cancellationToken).ConfigureAwait(false);
        if (pdfStream == null)
            throw new NotFoundException($"PDF file not found in storage: {fileId}/{bucket}");

        await using (pdfStream.ConfigureAwait(false))
        {
            var pdfBytes = await ReadStreamAsync(pdfStream, cancellationToken).ConfigureAwait(false);
            return await ExtractPageImageAsync(pdfBytes, pdfDoc.FileName.Value, query.PageNumber, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<byte[]> ExtractPageImageAsync(
        byte[] pdfBytes,
        string fileName,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("SmolDoclingService");

        using var pdfContent = new ByteArrayContent(pdfBytes);
        pdfContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(pdfContent, "file", fileName);

        var url = $"/api/v1/page-image?page_number={pageNumber}";

        _logger.LogDebug(
            "Calling SmolDocling page-image: PdfDocumentId=..., page={Page}",
            pageNumber);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(url, form, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Issue #3578: the container is simply not deployed here (staging omits smoldocling —
            // the 256M model is impractical on CPU). A transport failure is not a server bug.
            throw ServiceUnavailable(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces its own timeout as TaskCanceledException. The `when` guard is what
            // keeps a genuinely cancelled caller (client disconnected) from being reported as an
            // outage — that case must keep propagating as cancellation.
            throw ServiceUnavailable(ex);
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new NotFoundException($"Page {pageNumber} not found in PDF");

            // Upstream 5xx: the dependency is reachable but broken — still "unavailable" to the caller.
            // Upstream 4xx deliberately falls through to EnsureSuccessStatusCode: it means WE sent a
            // bad request, which is a real bug and must not be buried under a 503.
            if ((int)response.StatusCode >= 500)
                throw ServiceUnavailable(null, response.StatusCode);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Issue #3578 — 503 instead of 500 when the page-image dependency cannot serve the request.
    /// The message names the missing service so whoever hits it does not have to read container
    /// logs to find out a container is absent.
    /// </summary>
    private ExternalServiceException ServiceUnavailable(
        Exception? inner,
        System.Net.HttpStatusCode? upstreamStatus = null)
    {
        _logger.LogWarning(
            inner,
            "SmolDocling page-image unavailable (upstreamStatus={UpstreamStatus})",
            upstreamStatus);

        return new ExternalServiceException(
            "Page preview requires the SmolDocling service, which is not available in this environment.",
            "page_image_service_unavailable",
            inner);
    }

    private static string ExtractFileIdFromPath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var parts = fileName.Split('_');
        return parts.Length > 1 ? parts[0] : string.Empty;
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
