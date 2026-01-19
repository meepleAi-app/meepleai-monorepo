using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Events;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Services;

/// <summary>
/// Unit tests for ReportGeneratorService
/// ISSUE-919: Comprehensive test coverage for report generation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ReportGeneratorServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ReportGeneratorService>> _loggerMock;
    private readonly ReportGeneratorService _sut;

    public ReportGeneratorServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<ReportGeneratorService>>();
        _sut = new ReportGeneratorService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GenerateAsync Tests

    [Theory]
    [InlineData(ReportTemplate.SystemHealth, ReportFormat.Csv)]
    [InlineData(ReportTemplate.UserActivity, ReportFormat.Json)]
    [InlineData(ReportTemplate.AIUsage, ReportFormat.Pdf)]
    [InlineData(ReportTemplate.ContentMetrics, ReportFormat.Csv)]
    public async Task GenerateAsync_ValidTemplateAndFormat_ReturnsReportData(
        ReportTemplate template,
        ReportFormat format)
    {
        var parameters = CreateValidParameters(template);

        var result = await _sut.GenerateAsync(template, format, parameters, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        Assert.NotNull(result.FileName);
        Assert.True(result.FileSizeBytes > 0);
        Assert.Contains(template.ToString(), result.FileName);
        Assert.Contains(format.ToString().ToLowerInvariant(), result.FileName.ToLowerInvariant());
    }

    [Fact]
    public async Task GenerateAsync_SystemHealthReport_ContainsExpectedMetrics()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("\"uptime\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"errorRate\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"responseTime\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_UserActivityReport_ContainsUserMetrics()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("\"activeUsers\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"totalLogins\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_AIUsageReport_ContainsCostMetrics()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("\"totalCost\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"tokenUsage\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ContentMetricsReport_ContainsDocumentStats()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("\"totalDocuments\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"vectorEmbeddings\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_CsvFormat_ProducesValidCsv()
    {
        var parameters = CreateValidParameters(ReportTemplate.SystemHealth);

        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Csv,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains(",", content);
        Assert.Contains("\n", content);
        Assert.EndsWith(".csv", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_JsonFormat_ProducesValidJson()
    {
        var parameters = CreateValidParameters(ReportTemplate.UserActivity);

        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            parameters,
            CancellationToken.None);

        var content = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.StartsWith("{", content.TrimStart());
        Assert.EndsWith("}", content.TrimEnd());
        Assert.EndsWith(".json", result.FileName);
    }

    [Fact]
    public async Task GenerateAsync_PdfFormat_ProducesPdfFile()
    {
        var parameters = CreateValidParameters(ReportTemplate.AIUsage);

        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Pdf,
            parameters,
            CancellationToken.None);

        Assert.True(result.Content.Length >= 4);
        Assert.Equal(0x25, result.Content[0]); // %
        Assert.Equal(0x50, result.Content[1]); // P
        Assert.Equal(0x44, result.Content[2]); // D
        Assert.Equal(0x46, result.Content[3]); // F
        Assert.EndsWith(".pdf", result.FileName);
        Assert.True(result.FileSizeBytes > 1000);
    }

    [Fact]
    public async Task GenerateAsync_InvalidTemplate_ThrowsArgumentException()
    {
        var invalidTemplate = (ReportTemplate)999;
        var parameters = new Dictionary<string, object>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _sut.GenerateAsync(invalidTemplate, ReportFormat.Csv, parameters, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_InvalidParameters_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>
        {
            ["invalidParam"] = "value"
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_MissingDateRange_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _sut.GenerateAsync(ReportTemplate.UserActivity, ReportFormat.Csv, parameters, CancellationToken.None));

        Assert.Contains("startDate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var parameters = CreateValidParameters(ReportTemplate.SystemHealth);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, cts.Token));
    }

    #endregion

    #region ValidateParameters Tests

    [Fact]
    public void ValidateParameters_ValidDateRange_ReturnsValid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateParameters_MissingStartDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("startDate", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_MissingEndDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7)
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("endDate", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_EndDateBeforeStartDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow,
            ["endDate"] = DateTime.UtcNow.AddDays(-7)
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.ContentMetrics, parameters);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("after", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_DateRangeTooLarge_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddYears(-2),
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("range", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Helper Methods

    private static IReadOnlyDictionary<string, object> CreateValidParameters(ReportTemplate template)
    {
        return new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };
    }

    #endregion
}