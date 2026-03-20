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
using FluentAssertions;
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

        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().NotBeNull();
        (result.FileSizeBytes > 0).Should().BeTrue();
        result.FileName.Should().Contain(template.ToString());
        result.FileName.ToLowerInvariant().Should().Contain(format.ToString().ToLowerInvariant());
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
        content.Should().ContainEquivalentOf("\"uptime\"");
        content.Should().ContainEquivalentOf("\"errorRate\"");
        content.Should().ContainEquivalentOf("\"responseTime\"");
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
        content.Should().ContainEquivalentOf("\"activeUsers\"");
        content.Should().ContainEquivalentOf("\"totalLogins\"");
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
        content.Should().ContainEquivalentOf("\"totalCost\"");
        content.Should().ContainEquivalentOf("\"tokenUsage\"");
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
        content.Should().ContainEquivalentOf("\"totalDocuments\"");
        content.Should().ContainEquivalentOf("\"vectorEmbeddings\"");
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
        content.Should().Contain(",");
        content.Should().Contain("\n");
        result.FileName.Should().EndWith(".csv");
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
        content.TrimStart().Should().StartWith("{");
        content.TrimEnd().Should().EndWith("}");
        result.FileName.Should().EndWith(".json");
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

        (result.Content.Length >= 4).Should().BeTrue();
        result.Content[0].Should().Be(0x25); // %
        result.Content[1].Should().Be(0x50); // P
        result.Content[2].Should().Be(0x44); // D
        result.Content[3].Should().Be(0x46); // F
        result.FileName.Should().EndWith(".pdf");
        (result.FileSizeBytes > 1000).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_InvalidTemplate_ThrowsArgumentException()
    {
        var invalidTemplate = (ReportTemplate)999;
        var parameters = new Dictionary<string, object>();

        var act = async () =>
            await _sut.GenerateAsync(invalidTemplate, ReportFormat.Csv, parameters, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GenerateAsync_InvalidParameters_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>
        {
            ["invalidParam"] = "value"
        };

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateAsync_MissingDateRange_ThrowsArgumentException()
    {
        var parameters = new Dictionary<string, object>();

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.UserActivity, ReportFormat.Csv, parameters, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().ContainEquivalentOf("startDate");
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var parameters = CreateValidParameters(ReportTemplate.SystemHealth);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
            await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
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

        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateParameters_MissingStartDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["endDate"] = DateTime.UtcNow
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("startDate");
    }

    [Fact]
    public void ValidateParameters_MissingEndDate_ReturnsInvalid()
    {
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7)
        };

        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("endDate");
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

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("after");
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

        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().ContainEquivalentOf("range");
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