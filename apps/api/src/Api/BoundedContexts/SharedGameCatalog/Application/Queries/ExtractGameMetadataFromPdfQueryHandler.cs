using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for ExtractGameMetadataFromPdfQuery.
/// Extracts structured game metadata from uploaded PDF using SmolDocling OCR + AI parsing.
/// Returns GameMetadataDto with confidence scoring based on extraction quality and field completeness.
/// Issue #4155: Extract Game Metadata Query
/// </summary>
internal sealed class ExtractGameMetadataFromPdfQueryHandler : IQueryHandler<ExtractGameMetadataFromPdfQuery, GameMetadataDto>
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

        If a field cannot be determined from the text, return null for that field.
        Be conservative - only extract information you are confident about.
        """;

    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILlmService _llmService;
    private readonly ILogger<ExtractGameMetadataFromPdfQueryHandler> _logger;

    public ExtractGameMetadataFromPdfQueryHandler(
        IBlobStorageService blobStorageService,
        IPdfTextExtractor pdfTextExtractor,
        ILlmService llmService,
        ILogger<ExtractGameMetadataFromPdfQueryHandler> logger)
    {
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameMetadataDto> Handle(
        ExtractGameMetadataFromPdfQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation(
            "[{RequestId}] Starting game metadata extraction for user {UserId} from file {FilePath}",
            requestId, query.UserId, query.FilePath);

        try
        {
            // Step 1: Extract fileId from FilePath (format: {fileId}_{timestamp}.pdf)
            var fileId = ExtractFileIdFromPath(query.FilePath);
            if (string.IsNullOrEmpty(fileId))
            {
                _logger.LogWarning(
                    "[{RequestId}] Invalid file path format: {FilePath}",
                    requestId, query.FilePath);
                return GameMetadataDto.CreateEmpty();
            }

            // Step 2: Retrieve PDF from blob storage
            var pdfStream = await RetrievePdfFromStorageAsync(fileId, requestId, cancellationToken)
                .ConfigureAwait(false);

            if (pdfStream == null)
            {
                return GameMetadataDto.CreateEmpty();
            }

            try
            {
                // Step 3: Extract text using SmolDocling
                var extractionResult = await ExtractTextFromPdfAsync(pdfStream, requestId, cancellationToken)
                    .ConfigureAwait(false);

                if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.ExtractedText))
                {
                    _logger.LogWarning(
                        "[{RequestId}] Text extraction failed: {Error}",
                        requestId, extractionResult.ErrorMessage);
                    return GameMetadataDto.CreateEmpty();
                }

                // Step 4: AI parsing to structured metadata
                var metadata = await ParseMetadataWithAiAsync(
                    extractionResult.ExtractedText,
                    requestId,
                    cancellationToken).ConfigureAwait(false);

                if (metadata == null)
                {
                    return GameMetadataDto.CreateEmpty();
                }

                // Step 5: Calculate confidence score
                var confidenceScore = CalculateConfidenceScore(extractionResult, metadata);

                _logger.LogInformation(
                    "[{RequestId}] Metadata extraction completed: Title={Title}, Confidence={Confidence:F2}",
                    requestId, metadata.Title ?? "N/A", confidenceScore);

                return metadata with { ConfidenceScore = confidenceScore };
            }
            finally
            {
                await pdfStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "[{RequestId}] Metadata extraction cancelled", requestId);
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // QUERY HANDLER PATTERN: Graceful degradation
        // Returns empty metadata instead of throwing to allow wizard flow to continue.
        // User sees confidence=0.0 and can manually enter data.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] Unexpected error during metadata extraction for file {FilePath}",
                requestId, query.FilePath);
            return GameMetadataDto.CreateEmpty();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Extracts fileId from storage path format: {fileId}_{timestamp}.pdf
    /// </summary>
    private static string ExtractFileIdFromPath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var parts = fileName.Split('_');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    /// <summary>
    /// Retrieves PDF stream from blob storage (wizard-temp bucket)
    /// </summary>
    private async Task<Stream?> RetrievePdfFromStorageAsync(
        string fileId,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[{RequestId}] Retrieving PDF from storage: {FileId}", requestId, fileId);

            var stream = await _blobStorageService.RetrieveAsync(fileId, "wizard-temp", cancellationToken)
                .ConfigureAwait(false);

            if (stream == null)
            {
                _logger.LogWarning("[{RequestId}] PDF not found in storage: {FileId}", requestId, fileId);
            }

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] Failed to retrieve PDF from storage: {FileId}",
                requestId, fileId);
            return null;
        }
    }

    /// <summary>
    /// Extracts text from PDF using SmolDocling VLM extraction
    /// </summary>
    private async Task<TextExtractionResult> ExtractTextFromPdfAsync(
        Stream pdfStream,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[{RequestId}] Starting SmolDocling text extraction", requestId);

            var result = await _pdfTextExtractor.ExtractTextAsync(
                pdfStream,
                enableOcrFallback: true,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "[{RequestId}] Text extraction result: Success={Success}, Quality={Quality}, Pages={Pages}, Chars={Chars}",
                requestId, result.Success, result.Quality, result.PageCount, result.CharacterCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Text extraction failed", requestId);
            return TextExtractionResult.CreateFailure($"Extraction error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses extracted text into structured GameMetadataDto using AI
    /// </summary>
    private async Task<GameMetadataDto?> ParseMetadataWithAiAsync(
        string extractedText,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = SystemPromptTemplate;
            var userPrompt = BuildUserPrompt(extractedText);

            _logger.LogDebug(
                "[{RequestId}] Sending {CharCount} chars to LLM for metadata parsing",
                requestId, extractedText.Length);

            var result = await _llmService.GenerateJsonAsync<GameMetadataDto>(
                systemPrompt,
                userPrompt,
                cancellationToken).ConfigureAwait(false);

            if (result == null)
            {
                _logger.LogWarning("[{RequestId}] AI parsing returned null result", requestId);
                return null;
            }

            _logger.LogDebug(
                "[{RequestId}] AI parsing completed: Title={Title}, Year={Year}, Players={MinPlayers}-{MaxPlayers}",
                requestId, result.Title ?? "N/A", result.Year?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
                result.MinPlayers?.ToString(CultureInfo.InvariantCulture) ?? "?", result.MaxPlayers?.ToString(CultureInfo.InvariantCulture) ?? "?");

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[{RequestId}] Failed to deserialize AI response", requestId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] AI parsing failed", requestId);
            return null;
        }
    }

    /// <summary>
    /// Builds user prompt with extracted text
    /// </summary>
    private static string BuildUserPrompt(string extractedText)
    {
        // Truncate text if too long (AI context limits)
        const int maxTextLength = 8000;
        var truncatedText = extractedText.Length > maxTextLength
            ? extractedText[..maxTextLength] + "...[truncated]"
            : extractedText;

        return $"""
            Extract game metadata from this rulebook:

            {truncatedText}

            Return JSON with: Title, Year, MinPlayers, MaxPlayers, PlayingTime, MinAge, Description.
            Use null for fields that cannot be determined.
            """;
    }

    /// <summary>
    /// Calculates confidence score (0.0-1.0) based on extraction quality and field completeness
    /// </summary>
    /// <remarks>
    /// Score calculation:
    /// - Base confidence (0.0-0.5): SmolDocling quality score mapped to 0-50%
    /// - Field completeness (0.0-0.5): Percentage of non-null fields
    /// - Total: base + fields (max 1.0)
    /// </remarks>
    private static double CalculateConfidenceScore(TextExtractionResult extractionResult, GameMetadataDto metadata)
    {
        // Count populated fields
        var fieldsPopulated = CountPopulatedFields(metadata);

        // CRITICAL: Zero usable data = zero confidence (regardless of OCR quality)
        // If no fields were extracted, the result is unusable even if OCR quality was high
        if (fieldsPopulated == 0)
        {
            return 0.0;
        }

        // Base confidence from SmolDocling quality (0.0-0.5 scale)
        var baseConfidence = MapQualityToConfidence(extractionResult.Quality) * 0.5;

        // Field completeness score (0.0-0.5 scale)
        const int totalFields = 7; // Title, Year, MinPlayers, MaxPlayers, PlayingTime, MinAge, Description
        var fieldCompleteness = (fieldsPopulated / (double)totalFields) * 0.5;

        var totalConfidence = baseConfidence + fieldCompleteness;

        // Clamp to [0.0, 1.0]
        return Math.Clamp(totalConfidence, 0.0, 1.0);
    }

    /// <summary>
    /// Maps ExtractionQuality enum to confidence score (0.0-1.0)
    /// </summary>
    private static double MapQualityToConfidence(ExtractionQuality quality)
    {
        return quality switch
        {
            ExtractionQuality.High => 1.0,
            ExtractionQuality.Medium => 0.75,
            ExtractionQuality.Low => 0.5,
            ExtractionQuality.VeryLow => 0.25,
            _ => 0.0
        };
    }

    /// <summary>
    /// Counts number of populated (non-null, non-empty) fields in metadata
    /// </summary>
    private static int CountPopulatedFields(GameMetadataDto metadata)
    {
        var count = 0;

        if (!string.IsNullOrWhiteSpace(metadata.Title)) count++;
        if (metadata.Year.HasValue) count++;
        if (metadata.MinPlayers.HasValue) count++;
        if (metadata.MaxPlayers.HasValue) count++;
        if (metadata.PlayingTime.HasValue) count++;
        if (metadata.MinAge.HasValue) count++;
        if (!string.IsNullOrWhiteSpace(metadata.Description)) count++;

        return count;
    }
}
