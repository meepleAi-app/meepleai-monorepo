using System.Globalization;
using System.Text.RegularExpressions;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Extracts BGG games from PDF using UnstructuredService orchestrator
/// </summary>
internal sealed partial class BggGameExtractor : IBggGameExtractor
{
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ILogger<BggGameExtractor> _logger;

    // Regex for CSV format: "Nome Gioco,ID BGG"
    // Matches lines like: "Brass: Birmingham,224517" or "7 Wonders,68448"
    [GeneratedRegex(@"^(.+?),(\d+)$", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CsvLineRegex();

    public BggGameExtractor(
        IPdfTextExtractor pdfTextExtractor,
        ILogger<BggGameExtractor> logger)
    {
        _pdfTextExtractor = pdfTextExtractor;
        _logger = logger;
    }

    public async Task<List<BggGameDto>> ExtractGamesAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pdfFilePath);

        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");
        }

        _logger.LogInformation("Starting BGG games extraction from PDF: {PdfFilePath}", pdfFilePath);

        // Step 1: Extract text from PDF using orchestrator (quality-based fallback)
        string extractedText;
        var fileStream = File.OpenRead(pdfFilePath);
        await using (fileStream.ConfigureAwait(false))
        {
            var extractionResult = await _pdfTextExtractor.ExtractTextAsync(
                fileStream,
                enableOcrFallback: true,
                cancellationToken).ConfigureAwait(false);

            if (!extractionResult.Success || string.IsNullOrWhiteSpace(extractionResult.ExtractedText))
            {
                _logger.LogError(
                    "PDF text extraction failed: {ErrorMessage}",
                    extractionResult.ErrorMessage ?? "Unknown error");
                throw new InvalidOperationException(
                    $"Failed to extract text from PDF: {extractionResult.ErrorMessage}");
            }

            extractedText = extractionResult.ExtractedText;
            _logger.LogInformation(
                "PDF text extraction successful. Quality: {Quality}, Length: {Length} chars",
                extractionResult.Quality,
                extractedText.Length);
        }

        // Step 1.5: DoS protection - validate line count before regex processing
        var lines = extractedText.Split('\n');
        if (lines.Length > 10_000)
        {
            _logger.LogError(
                "PDF contains too many lines for processing: {LineCount}. Maximum allowed: 10,000",
                lines.Length);
            throw new InvalidOperationException(
                $"PDF contains too many lines ({lines.Length}). Maximum allowed: 10,000");
        }

        // Step 2: Parse CSV lines with regex
        var games = new List<BggGameDto>();
        var matches = CsvLineRegex().Matches(extractedText);

        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 3)
                continue;

            var gameName = match.Groups[1].Value.Trim();
            var bggIdString = match.Groups[2].Value;

            // Validation: Parse BggId
            if (!int.TryParse(bggIdString, CultureInfo.InvariantCulture, out var bggId))
            {
                _logger.LogWarning(
                    "Skipping line with invalid BGG ID: {GameName},{BggIdString}",
                    gameName,
                    bggIdString);
                continue;
            }

            // Validation: BggId > 0, Name min 2 chars
            if (bggId <= 0 || gameName.Length < 2)
            {
                _logger.LogWarning(
                    "Skipping invalid game: Name='{GameName}' (len={NameLength}), BggId={BggId}",
                    gameName,
                    gameName.Length,
                    bggId);
                continue;
            }

            games.Add(new BggGameDto(gameName, bggId));
            _logger.LogDebug("Extracted game: {GameName} (ID: {BggId})", gameName, bggId);
        }

        _logger.LogInformation(
            "BGG games extraction complete. Total games: {GameCount}, Matches found: {MatchCount}",
            games.Count,
            matches.Count);

        if (games.Count == 0)
        {
            _logger.LogWarning("No valid games extracted from PDF. Check format and validation rules.");
        }

        return games;
    }
}
