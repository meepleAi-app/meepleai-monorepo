using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

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
            throw new ArgumentOutOfRangeException(nameof(query.PageNumber), "Page number must be >= 1");

        var pdfDoc = await _pdfRepo.GetByIdAsync(query.PdfDocumentId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
            throw new NotFoundException($"PdfDocument {query.PdfDocumentId} not found");

        // Resolve storage bucket
        var bucket = (pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? "wizard-temp";

        // Extract file ID from stored path
        var fileId = ExtractFileIdFromPath(pdfDoc.FilePath);
        if (string.IsNullOrEmpty(fileId))
            throw new InvalidOperationException($"Cannot extract fileId from path: {pdfDoc.FilePath}");

        // Retrieve PDF bytes from blob storage
        var pdfStream = await _blobStorageService.RetrieveAsync(fileId, bucket, cancellationToken).ConfigureAwait(false);
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
        client.Timeout = TimeSpan.FromSeconds(30);

        using var pdfContent = new ByteArrayContent(pdfBytes);
        pdfContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        using var form = new MultipartFormDataContent();
        form.Add(pdfContent, "file", fileName);

        var url = $"/api/v1/page-image?page_number={pageNumber}";

        _logger.LogDebug(
            "Calling SmolDocling page-image: PdfDocumentId=..., page={Page}",
            pageNumber);

        using var response = await client.PostAsync(url, form, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new NotFoundException($"Page {pageNumber} not found in PDF");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ExtractFileIdFromPath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var parts = fileName.Split('_');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
