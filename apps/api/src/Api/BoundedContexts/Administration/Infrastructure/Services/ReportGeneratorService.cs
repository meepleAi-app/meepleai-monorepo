using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Implementation of IReportGeneratorService
/// ISSUE-916: Generates reports from 4 templates in PDF/CSV/JSON formats
/// </summary>
internal sealed partial class ReportGeneratorService : IReportGeneratorService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<ReportGeneratorService> _logger;
    private readonly Dictionary<ReportFormat, IReportFormatter> _formatters;

    public ReportGeneratorService(
        MeepleAiDbContext dbContext,
        ILogger<ReportGeneratorService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize formatters
        _formatters = new Dictionary<ReportFormat, IReportFormatter>
        {
            [ReportFormat.Csv] = new CsvReportFormatter(),
            [ReportFormat.Json] = new JsonReportFormatter(),
            [ReportFormat.Pdf] = new PdfReportFormatter() // ISSUE-917: QuestPDF integration complete
        };
    }

    public async Task<ReportData> GenerateAsync(
        ReportTemplate template,
        ReportFormat format,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating {Template} report in {Format} format",
            template, format);

        // Validate parameters
        var (isValid, errorMessage) = ValidateParameters(template, parameters);
        if (!isValid)
        {
            throw new ArgumentException(errorMessage, nameof(parameters));
        }

        // Generate content based on template
        var content = template switch
        {
            ReportTemplate.SystemHealth => await GenerateSystemHealthReportAsync(parameters, cancellationToken).ConfigureAwait(false),
            ReportTemplate.UserActivity => await GenerateUserActivityReportAsync(parameters, cancellationToken).ConfigureAwait(false),
            ReportTemplate.AIUsage => await GenerateAIUsageReportAsync(parameters, cancellationToken).ConfigureAwait(false),
            ReportTemplate.ContentMetrics => await GenerateContentMetricsReportAsync(parameters, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown report template")
        };

        // Format the content
        var formatter = _formatters[format];
        var bytes = formatter.Format(content);
        var fileName = $"{template}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{formatter.GetFileExtension()}";

        _logger.LogInformation(
            "Report generated: {FileName} ({Size} bytes)",
            fileName, bytes.Length);

        return new ReportData(
            Content: bytes,
            FileName: fileName,
            FileSizeBytes: bytes.Length,
            Metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["template"] = template.ToString(),
                ["format"] = format.ToString(),
                ["generatedAt"] = DateTime.UtcNow
            });
    }

    public (bool IsValid, string? ErrorMessage) ValidateParameters(
        ReportTemplate template,
        IReadOnlyDictionary<string, object> parameters)
    {
        return template switch
        {
            ReportTemplate.SystemHealth => ValidateSystemHealthParameters(parameters),
            ReportTemplate.UserActivity => ValidateUserActivityParameters(parameters),
            ReportTemplate.AIUsage => ValidateAIUsageParameters(parameters),
            ReportTemplate.ContentMetrics => ValidateContentMetricsParameters(parameters),
            _ => (false, $"Unknown template: {template}")
        };
    }
}

