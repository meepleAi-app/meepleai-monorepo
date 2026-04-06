using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for ExtractGameMetadataFromPdfByPdfIdQuery.
/// Looks up the PdfDocument by ID to resolve the storage bucket and file path,
/// then runs the same SmolDocling OCR + LLM pipeline as the FilePath-based variant.
/// </summary>
internal sealed class ExtractGameMetadataFromPdfByPdfIdQueryHandler
    : IQueryHandler<ExtractGameMetadataFromPdfByPdfIdQuery, GameMetadataDto>
{
    private static readonly string SystemPromptTemplate = """
        You are a board game metadata extractor. Your task is to extract structured information from board game rulebook text.
        Extract the following fields with high precision:
        - Title: The game's official title (string)
        - Year: Publication year (integer, e.g., 2024)
        - MinPlayers: Minimum number of players (integer)
        - MaxPlayers: Maximum number of players (integer)
        - PlayingTime: Average playing time in minutes (integer)
        - MinAge: Minimum recommended age (integer)
        - Description: Brief game description or overview (string, 1-3 sentences)
        - Publishers: List of publisher names (array of strings, e.g., ["CMON", "Stonemaier Games"])
        - Designers: List of game designer/author names (array of strings, e.g., ["Uwe Rosenberg"])
        - Categories: List of game categories (array of strings, e.g., ["Strategy", "Fantasy", "Family"])
        - Mechanics: List of game mechanics (array of strings, e.g., ["Deck Building", "Worker Placement", "Area Control"])

        If a field cannot be determined from the text, return null for that field (or empty array for list fields).
        Be conservative - only extract information you are confident about.
        """;

    private readonly IPdfDocumentRepository _pdfRepo;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILlmService _llmService;
    private readonly ILogger<ExtractGameMetadataFromPdfByPdfIdQueryHandler> _logger;

    public ExtractGameMetadataFromPdfByPdfIdQueryHandler(
        IPdfDocumentRepository pdfRepo,
        IBlobStorageService blobStorageService,
        IPdfTextExtractor pdfTextExtractor,
        ILlmService llmService,
        ILogger<ExtractGameMetadataFromPdfByPdfIdQueryHandler> logger)
    {
        _pdfRepo = pdfRepo ?? throw new ArgumentNullException(nameof(pdfRepo));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameMetadataDto> Handle(
        ExtractGameMetadataFromPdfByPdfIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation(
            "[{RequestId}] Starting metadata extraction for PdfDocumentId={PdfDocumentId}, UserId={UserId}",
            requestId, query.PdfDocumentId, query.UserId);

        try
        {
            var pdfDoc = await _pdfRepo.GetByIdAsync(query.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);

            if (pdfDoc == null)
            {
                _logger.LogWarning("[{RequestId}] PdfDocument {PdfDocumentId} not found", requestId, query.PdfDocumentId);
                return GameMetadataDto.CreateEmpty();
            }

            // Resolve storage bucket: orphaned PDFs use "wizard-temp"
            var bucket = (pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? "wizard-temp";

            // Extract fileId from stored FilePath (format: {fileId}_{timestamp}.pdf)
            var fileId = ExtractFileIdFromPath(pdfDoc.FilePath);
            if (string.IsNullOrEmpty(fileId))
            {
                _logger.LogWarning("[{RequestId}] Could not extract fileId from FilePath={FilePath}", requestId, pdfDoc.FilePath);
                return GameMetadataDto.CreateEmpty();
            }

            var pdfStream = await RetrievePdfAsync(fileId, bucket, requestId, cancellationToken).ConfigureAwait(false);
            if (pdfStream == null)
                return GameMetadataDto.CreateEmpty();

            try
            {
                var extractionResult = await _pdfTextExtractor.ExtractTextAsync(
                    pdfStream, enableOcrFallback: true, cancellationToken).ConfigureAwait(false);

                if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.ExtractedText))
                {
                    _logger.LogWarning("[{RequestId}] Text extraction failed: {Error}", requestId, extractionResult.ErrorMessage);
                    return GameMetadataDto.CreateEmpty();
                }

                var metadata = await ParseWithAiAsync(extractionResult.ExtractedText, requestId, cancellationToken)
                    .ConfigureAwait(false);

                if (metadata == null)
                    return GameMetadataDto.CreateEmpty();

                var confidence = CalculateConfidence(extractionResult, metadata);

                _logger.LogInformation(
                    "[{RequestId}] Extraction done: Title={Title}, Confidence={Confidence:F2}",
                    requestId, metadata.Title ?? "N/A", confidence);

                return metadata with { ConfidenceScore = confidence };
            }
            finally
            {
                await pdfStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "[{RequestId}] Extraction cancelled", requestId);
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Unexpected error for PdfDocumentId={PdfDocumentId}", requestId, query.PdfDocumentId);
            return GameMetadataDto.CreateEmpty();
        }
#pragma warning restore CA1031
    }

    private static string ExtractFileIdFromPath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var parts = fileName.Split('_');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private async Task<Stream?> RetrievePdfAsync(string fileId, string bucket, string requestId, CancellationToken ct)
    {
        try
        {
            var stream = await _blobStorageService.RetrieveAsync(fileId, bucket, ct).ConfigureAwait(false);
            if (stream == null)
                _logger.LogWarning("[{RequestId}] PDF not found: fileId={FileId}, bucket={Bucket}", requestId, fileId, bucket);
            return stream;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Failed to retrieve PDF: {FileId}/{Bucket}", requestId, fileId, bucket);
            return null;
        }
#pragma warning restore CA1031
    }

    private async Task<GameMetadataDto?> ParseWithAiAsync(string text, string requestId, CancellationToken ct)
    {
        const int maxTextLength = 8000;
        var truncated = text.Length > maxTextLength ? text[..maxTextLength] + "...[truncated]" : text;
        var userPrompt = $"""
            Extract game metadata from this rulebook:

            {truncated}

            Return JSON with: Title, Year, MinPlayers, MaxPlayers, PlayingTime, MinAge, Description, Publishers, Designers, Categories, Mechanics.
            Use null for fields that cannot be determined. Use empty array [] for list fields with no data.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<GameMetadataDto>(
                SystemPromptTemplate, userPrompt, RequestSource.RagPipeline, ct).ConfigureAwait(false);

            if (result == null)
                _logger.LogWarning("[{RequestId}] AI parsing returned null", requestId);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[{RequestId}] JSON deserialization failed", requestId);
            return null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] AI parsing failed", requestId);
            return null;
        }
#pragma warning restore CA1031
    }

    private static double CalculateConfidence(TextExtractionResult extractionResult, GameMetadataDto metadata)
    {
        var populated = CountPopulated(metadata);
        if (populated == 0) return 0.0;

        var baseConf = MapQuality(extractionResult.Quality) * 0.5;
        const int total = 11;
        var fieldConf = (populated / (double)total) * 0.5;
        return Math.Clamp(baseConf + fieldConf, 0.0, 1.0);
    }

    private static double MapQuality(ExtractionQuality quality) => quality switch
    {
        ExtractionQuality.High => 1.0,
        ExtractionQuality.Medium => 0.75,
        ExtractionQuality.Low => 0.5,
        ExtractionQuality.VeryLow => 0.25,
        _ => 0.0
    };

    private static int CountPopulated(GameMetadataDto m)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(m.Title)) count++;
        if (m.Year.HasValue) count++;
        if (m.MinPlayers.HasValue) count++;
        if (m.MaxPlayers.HasValue) count++;
        if (m.PlayingTime.HasValue) count++;
        if (m.MinAge.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(m.Description)) count++;
        if (m.Publishers?.Count > 0) count++;
        if (m.Designers?.Count > 0) count++;
        if (m.Categories?.Count > 0) count++;
        if (m.Mechanics?.Count > 0) count++;
        return count;
    }
}
