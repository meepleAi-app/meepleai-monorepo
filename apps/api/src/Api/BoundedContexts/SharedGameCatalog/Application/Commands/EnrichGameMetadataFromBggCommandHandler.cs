using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for EnrichGameMetadataFromBggCommand.
/// Fetches BGG data, merges with PDF-extracted metadata, and detects conflicts.
/// Implements graceful fallback to PDF-only data if BGG enrichment fails.
/// Issue #4156: BGG Match and Enrichment Command
/// </summary>
internal sealed class EnrichGameMetadataFromBggCommandHandler
    : ICommandHandler<EnrichGameMetadataFromBggCommand, EnrichedGameDto>
{
    private readonly IBggApiClient _bggApiClient;
    private readonly ILogger<EnrichGameMetadataFromBggCommandHandler> _logger;

    public EnrichGameMetadataFromBggCommandHandler(
        IBggApiClient bggApiClient,
        ILogger<EnrichGameMetadataFromBggCommandHandler> logger)
    {
        _bggApiClient = bggApiClient ?? throw new ArgumentNullException(nameof(bggApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichedGameDto> Handle(
        EnrichGameMetadataFromBggCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdfMetadata = command.ExtractedMetadata;
        var bggId = command.BggId;

        // Attempt BGG enrichment with graceful fallback
        BggGameDetails? bggData = null;
        string? enrichmentWarning = null;
        var enrichmentSucceeded = false;

        try
        {
            bggData = await _bggApiClient.GetGameDetailsAsync(bggId, cancellationToken)
                .ConfigureAwait(false);
            enrichmentSucceeded = true;

            _logger.LogInformation(
                "Successfully fetched BGG data for BggId: {BggId}, Title: {Title}",
                bggId, bggData.Title);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex,
                "BGG API timeout for BggId: {BggId}. Falling back to PDF-only data.",
                bggId);
            enrichmentWarning = "BGG enrichment timed out. Using PDF-extracted data only.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "BGG API request failed for BggId: {BggId}. Falling back to PDF-only data.",
                bggId);
            enrichmentWarning = "BGG enrichment failed. Using PDF-extracted data only.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Unexpected error during BGG enrichment for BggId: {BggId}. Falling back to PDF-only data.",
                bggId);
            enrichmentWarning = "An unexpected error occurred during BGG enrichment. Using PDF-extracted data only.";
        }

        // Merge data and detect conflicts
        var (mergedData, conflicts) = MergeMetadata(pdfMetadata, bggData);

        return new EnrichedGameDto
        {
            Title = mergedData.Title,
            Year = mergedData.Year,
            MinPlayers = mergedData.MinPlayers,
            MaxPlayers = mergedData.MaxPlayers,
            PlayingTime = mergedData.PlayingTime,
            MinAge = mergedData.MinAge,
            Description = mergedData.Description,
            ComplexityRating = bggData?.ComplexityRating,
            ImageUrl = bggData?.ImageUrl,
            ThumbnailUrl = bggData?.ThumbnailUrl,
            AverageRating = bggData?.AverageRating,
            RankPosition = bggData?.RankPosition,
            BggId = bggId,
            PdfConfidenceScore = pdfMetadata.ConfidenceScore,
            Conflicts = conflicts,
            BggEnrichmentSucceeded = enrichmentSucceeded,
            EnrichmentWarning = enrichmentWarning
        };
    }

    /// <summary>
    /// Merges PDF and BGG metadata with conflict detection.
    /// Strategy:
    /// - If only one source has data: use that source
    /// - If both have data and differ: prefer BGG, add to conflicts list
    /// - If both have data and match: use value (no conflict)
    /// </summary>
    private (MergedMetadata Merged, List<MetadataConflict> Conflicts) MergeMetadata(
        GameMetadataDto pdfData,
        BggGameDetails? bggData)
    {
        var conflicts = new List<MetadataConflict>();

        // Title: BGG preferred if both exist
        var title = bggData?.Title ?? pdfData.Title ?? "Unknown Game";

        // Year: Merge with conflict detection
        var year = MergeField(
            pdfValue: pdfData.Year,
            bggValue: bggData?.YearPublished,
            fieldName: "Year",
            conflicts: conflicts);

        // MinPlayers: Merge with conflict detection
        var minPlayers = MergeField(
            pdfValue: pdfData.MinPlayers,
            bggValue: bggData?.MinPlayers,
            fieldName: "MinPlayers",
            conflicts: conflicts);

        // MaxPlayers: Merge with conflict detection
        var maxPlayers = MergeField(
            pdfValue: pdfData.MaxPlayers,
            bggValue: bggData?.MaxPlayers,
            fieldName: "MaxPlayers",
            conflicts: conflicts);

        // PlayingTime: Merge with conflict detection
        var playingTime = MergeField(
            pdfValue: pdfData.PlayingTime,
            bggValue: bggData?.PlayingTimeMinutes,
            fieldName: "PlayingTime",
            conflicts: conflicts);

        // MinAge: Merge with conflict detection (BGG doesn't have MinAge, so no conflict)
        var minAge = pdfData.MinAge;

        // Description: BGG preferred if both exist
        var description = bggData?.Description ?? pdfData.Description;

        return (
            new MergedMetadata(title, year, minPlayers, maxPlayers, playingTime, minAge, description),
            conflicts
        );
    }

    /// <summary>
    /// Merges a single nullable int field with conflict detection.
    /// Returns BGG value if both exist, adds conflict if values differ.
    /// </summary>
    private static int? MergeField(
        int? pdfValue,
        int? bggValue,
        string fieldName,
        List<MetadataConflict> conflicts)
    {
        // If only one source has data, use it
        if (pdfValue.HasValue && !bggValue.HasValue)
            return pdfValue;
        if (!pdfValue.HasValue && bggValue.HasValue)
            return bggValue;

        // If neither has data, return null
        if (!pdfValue.HasValue && !bggValue.HasValue)
            return null;

        // Both have data - check for conflict
        if (pdfValue!.Value != bggValue!.Value)
        {
            conflicts.Add(new MetadataConflict
            {
                FieldName = fieldName,
                BggValue = bggValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PdfValue = pdfValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }

        // Prefer BGG value
        return bggValue;
    }

    /// <summary>
    /// Internal record for merged metadata fields.
    /// </summary>
    private sealed record MergedMetadata(
        string Title,
        int? Year,
        int? MinPlayers,
        int? MaxPlayers,
        int? PlayingTime,
        int? MinAge,
        string? Description);
}
