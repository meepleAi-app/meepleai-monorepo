using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Api.Infrastructure;
using Api.Tests.TestInfrastructure.InMemory;
using Microsoft.Extensions.Logging;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Factory for creating ReportGeneratorService with test dependencies
/// ISSUE-2601: Provides in-memory formatters for unit tests
/// </summary>
internal static class TestReportGeneratorServiceFactory
{
    /// <summary>
    /// Creates a ReportGeneratorService with in-memory formatters for unit tests
    /// </summary>
    public static TestableReportGeneratorService CreateWithInMemoryFormatters(
        MeepleAiDbContext dbContext,
        ILogger<TestableReportGeneratorService> logger)
    {
        var formatters = new Dictionary<ReportFormat, IReportFormatter>
        {
            [ReportFormat.Csv] = new InMemoryCsvReportFormatter(),
            [ReportFormat.Json] = new InMemoryJsonReportFormatter(),
            [ReportFormat.Pdf] = new InMemoryPdfReportFormatter()
        };

        return new TestableReportGeneratorService(dbContext, logger, formatters);
    }
}

/// <summary>
/// Testable version of ReportGeneratorService with injectable formatters
/// ISSUE-2601: Allows swapping formatters for unit vs integration tests
/// </summary>
internal sealed class TestableReportGeneratorService : IReportGeneratorService
{
    private readonly ILogger<TestableReportGeneratorService> _logger;
    private readonly Dictionary<ReportFormat, IReportFormatter> _formatters;

    public TestableReportGeneratorService(
        MeepleAiDbContext dbContext,
        ILogger<TestableReportGeneratorService> logger,
        Dictionary<ReportFormat, IReportFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _formatters = formatters ?? throw new ArgumentNullException(nameof(formatters));
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

        // Generate minimal test content
        var content = new ReportContent(
            Title: $"{template} Report",
            Description: "Test report",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal),
            Sections: new List<ReportSection>());

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
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
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
        // Basic validation matching the real service
        return template switch
        {
            ReportTemplate.SystemHealth => ValidateSystemHealthParameters(parameters),
            ReportTemplate.UserActivity => ValidateUserActivityParameters(parameters),
            ReportTemplate.AIUsage => ValidateAIUsageParameters(parameters),
            ReportTemplate.ContentMetrics => ValidateContentMetricsParameters(parameters),
            _ => (true, null)
        };
    }

    private static (bool IsValid, string? ErrorMessage) ValidateSystemHealthParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        // Optional: hours parameter (default: 24) - matches production behavior
        var hours = 24; // default value

        if (parameters.TryGetValue("hours", out var hoursObj))
        {
            if (!int.TryParse(hoursObj.ToString(), out hours))
            {
                return (false, "Parameter 'hours' must be an integer");
            }

            if (hours < 1 || hours > 720)
            {
                return (false, "Parameter 'hours' must be between 1 and 720");
            }
        }

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateUserActivityParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("startDate", out var startDateObj) ||
            !parameters.TryGetValue("endDate", out var endDateObj))
        {
            return (false, "Parameters 'startDate' and 'endDate' are required");
        }

        if (startDateObj is not DateTime || endDateObj is not DateTime)
        {
            return (false, "Parameters 'startDate' and 'endDate' must be DateTime");
        }

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateAIUsageParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("startDate", out var startDateObj) ||
            !parameters.TryGetValue("endDate", out var endDateObj))
        {
            return (false, "Parameters 'startDate' and 'endDate' are required");
        }

        if (startDateObj is not DateTime startDate || endDateObj is not DateTime endDate)
        {
            return (false, "Parameters 'startDate' and 'endDate' must be DateTime");
        }

        if ((endDate - startDate).TotalDays > 365)
        {
            return (false, "Date range cannot exceed 365 days");
        }

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateContentMetricsParameters(
        IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("startDate", out var startDateObj) ||
            !parameters.TryGetValue("endDate", out var endDateObj))
        {
            return (false, "Parameters 'startDate' and 'endDate' are required");
        }

        if (startDateObj is not DateTime || endDateObj is not DateTime)
        {
            return (false, "Parameters 'startDate' and 'endDate' must be DateTime");
        }

        return (true, null);
    }
}
