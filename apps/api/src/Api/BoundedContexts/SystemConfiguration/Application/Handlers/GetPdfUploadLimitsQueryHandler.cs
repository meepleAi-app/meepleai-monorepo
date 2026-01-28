using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of PDF upload limits configuration.
/// Returns default values if configurations not found in database.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal class GetPdfUploadLimitsQueryHandler : IQueryHandler<GetPdfUploadLimitsQuery, PdfUploadLimitsDto>
{
    private readonly IConfigurationService _configService;

    private const string MaxFileSizeKey = "PdfUpload:MaxFileSizeBytes";
    private const string MaxPagesKey = "PdfUpload:MaxPagesPerDocument";
    private const string MaxDocumentsKey = "PdfUpload:MaxDocumentsPerGame";
    private const string AllowedMimeTypesKey = "PdfUpload:AllowedMimeTypes";

    // Default values: 100MB, 500 pages, 10 documents, PDF only
    private const long DefaultMaxFileSizeBytes = 104857600; // 100MB
    private const int DefaultMaxPagesPerDocument = 500;
    private const int DefaultMaxDocumentsPerGame = 10;
    private static readonly string[] DefaultAllowedMimeTypes = ["application/pdf"];

    public GetPdfUploadLimitsQueryHandler(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<PdfUploadLimitsDto> Handle(GetPdfUploadLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch all configurations in parallel for efficiency
        var maxFileSizeTask = _configService.GetConfigurationByKeyAsync(MaxFileSizeKey, null, cancellationToken);
        var maxPagesTask = _configService.GetConfigurationByKeyAsync(MaxPagesKey, null, cancellationToken);
        var maxDocumentsTask = _configService.GetConfigurationByKeyAsync(MaxDocumentsKey, null, cancellationToken);
        var mimeTypesTask = _configService.GetConfigurationByKeyAsync(AllowedMimeTypesKey, null, cancellationToken);

        await Task.WhenAll(maxFileSizeTask, maxPagesTask, maxDocumentsTask, mimeTypesTask).ConfigureAwait(false);

        var maxFileSizeConfig = await maxFileSizeTask.ConfigureAwait(false);
        var maxPagesConfig = await maxPagesTask.ConfigureAwait(false);
        var maxDocumentsConfig = await maxDocumentsTask.ConfigureAwait(false);
        var mimeTypesConfig = await mimeTypesTask.ConfigureAwait(false);

        // Parse values with defaults
        var maxFileSizeBytes = ParseLongOrDefault(maxFileSizeConfig?.Value, DefaultMaxFileSizeBytes);
        var maxPagesPerDocument = ParseIntOrDefault(maxPagesConfig?.Value, DefaultMaxPagesPerDocument);
        var maxDocumentsPerGame = ParseIntOrDefault(maxDocumentsConfig?.Value, DefaultMaxDocumentsPerGame);
        var allowedMimeTypes = ParseMimeTypesOrDefault(mimeTypesConfig?.Value);

        // Determine most recent update timestamp
        var allConfigs = new[] { maxFileSizeConfig, maxPagesConfig, maxDocumentsConfig, mimeTypesConfig }
            .Where(c => c != null)
            .ToList();

        var lastUpdatedAt = allConfigs.Count > 0
            ? allConfigs.Max(c => c!.UpdatedAt)
            : DateTime.UtcNow;

        var lastUpdatedByUserId = allConfigs
            .OrderByDescending(c => c!.UpdatedAt)
            .FirstOrDefault()?.UpdatedByUserId;

        return new PdfUploadLimitsDto(
            MaxFileSizeBytes: maxFileSizeBytes,
            MaxPagesPerDocument: maxPagesPerDocument,
            MaxDocumentsPerGame: maxDocumentsPerGame,
            AllowedMimeTypes: allowedMimeTypes,
            LastUpdatedAt: lastUpdatedAt,
            LastUpdatedByUserId: lastUpdatedByUserId
        );
    }

    private static long ParseLongOrDefault(string? value, long defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ParseIntOrDefault(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string[] ParseMimeTypesOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultAllowedMimeTypes;

        // MIME types stored as comma-separated string
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
