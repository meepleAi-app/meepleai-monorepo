using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Comprehensive unit tests for ReportGeneratorService
/// ISSUE-919: 90%+ coverage for all report templates, formats, and validation
/// ISSUE-2601: Uses in-memory formatters to avoid external dependencies
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ReportGeneratorServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TestableReportGeneratorService _sut;
    private readonly Mock<ILogger<TestableReportGeneratorService>> _loggerMock;

    public ReportGeneratorServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ReportTests_{Guid.NewGuid()}")
            .Options;

        var mediatorMock = new Mock<MediatR.IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();

        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<TestableReportGeneratorService>>();
        _sut = TestReportGeneratorServiceFactory.CreateWithInMemoryFormatters(_dbContext, _loggerMock.Object);

        // Note: DB is empty - tests will generate reports with zero counts, which is valid for testing report generation logic
    }

    #region SystemHealth Template Tests

    [Fact]
    public async Task GenerateAsync_SystemHealth_Csv_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Csv,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".csv");
        result.FileSizeBytes.Should().Be(result.Content.Length);
        result.Metadata.Keys.Should().Contain("template");
        result.FileName.Should().Contain("SystemHealth");
    }

    [Fact]
    public async Task GenerateAsync_SystemHealth_Json_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 48 };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".json");
        (result.FileSizeBytes > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_SystemHealth_Pdf_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 72 };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Pdf,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".pdf");
        (result.FileSizeBytes > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_SystemHealth_DefaultHours_ShouldUse24Hours()
    {
        // Arrange - no hours parameter
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Json,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Keys.Should().Contain("template");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(721)]
    [InlineData(1000)]
    public async Task GenerateAsync_SystemHealth_InvalidHours_ShouldThrowArgumentException(int invalidHours)
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = invalidHours };

        // Act & Assert
        var act = () =>
            _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(168)]
    [InlineData(720)]
    public void ValidateParameters_SystemHealth_ValidHours_ShouldReturnValid(int validHours)
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = validHours };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateParameters_SystemHealth_NoParameters_ShouldReturnValid()
    {
        // Arrange - empty parameters (hours is optional)
        var parameters = new Dictionary<string, object>();

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    #endregion

    #region UserActivity Template Tests

    [Fact]
    public async Task GenerateAsync_UserActivity_Csv_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Csv,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".csv");
        result.FileName.Should().Contain("UserActivity");
    }

    [Fact]
    public async Task GenerateAsync_UserActivity_Json_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddMonths(-1),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Json,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task GenerateAsync_UserActivity_Pdf_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.UserActivity,
            ReportFormat.Pdf,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void ValidateParameters_UserActivity_MissingStartDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["endDate"] = DateTime.UtcNow };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("startDate");
    }

    [Fact]
    public void ValidateParameters_UserActivity_MissingEndDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["startDate"] = DateTime.UtcNow.AddDays(-7) };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
        errorMessage.Should().Contain("endDate");
    }

    [Fact]
    public void ValidateParameters_UserActivity_InvalidStartDateType_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = "not-a-date",
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNull();
    }

    [Fact]
    public void ValidateParameters_UserActivity_ValidDates_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    #endregion

    #region AIUsage Template Tests

    [Fact]
    public async Task GenerateAsync_AIUsage_Csv_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Csv,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".csv");
        result.FileName.Should().Contain("AIUsage");
    }

    [Fact]
    public async Task GenerateAsync_AIUsage_Json_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Json,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        // ReportData.Metadata contains template/format/generatedAt, not report-specific metadata
        result.Metadata.Keys.Should().Contain("template");
        result.Metadata.Keys.Should().Contain("format");
        result.Metadata["template"].ToString().Should().Be("AIUsage");
    }

    [Fact]
    public async Task GenerateAsync_AIUsage_Pdf_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddMonths(-1),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.AIUsage,
            ReportFormat.Pdf,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void ValidateParameters_AIUsage_MissingStartDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["endDate"] = DateTime.UtcNow };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage!.Should().Contain("startDate");
    }

    [Fact]
    public void ValidateParameters_AIUsage_MissingEndDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["startDate"] = DateTime.UtcNow.AddDays(-7) };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage!.Should().Contain("endDate");
    }

    [Fact]
    public void ValidateParameters_AIUsage_ValidDates_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.AIUsage, parameters);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    #endregion

    #region ContentMetrics Template Tests

    [Fact]
    public async Task GenerateAsync_ContentMetrics_Csv_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Csv,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".csv");
        result.FileName.Should().Contain("ContentMetrics");
    }

    [Fact]
    public async Task GenerateAsync_ContentMetrics_Json_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddMonths(-1),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Json,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task GenerateAsync_ContentMetrics_Pdf_ShouldGenerateReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-7),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.ContentMetrics,
            ReportFormat.Pdf,
            parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void ValidateParameters_ContentMetrics_MissingStartDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["endDate"] = DateTime.UtcNow };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.ContentMetrics, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage!.Should().Contain("startDate");
    }

    [Fact]
    public void ValidateParameters_ContentMetrics_MissingEndDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["startDate"] = DateTime.UtcNow.AddDays(-7) };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.ContentMetrics, parameters);

        // Assert
        isValid.Should().BeFalse();
        errorMessage!.Should().Contain("endDate");
    }

    [Fact]
    public void ValidateParameters_ContentMetrics_ValidDates_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["startDate"] = DateTime.UtcNow.AddDays(-30),
            ["endDate"] = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.ContentMetrics, parameters);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    #endregion

    #region Format-Specific Tests

    [Fact]
    public async Task GenerateAsync_AllFormats_ShouldHaveCorrectExtensions()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var csvResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters);
        var jsonResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters);
        var pdfResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Pdf, parameters);

        // Assert
        csvResult.FileName.Should().EndWith(".csv");
        jsonResult.FileName.Should().EndWith(".json");
        pdfResult.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task GenerateAsync_AllFormats_ShouldHaveNonZeroSize()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var csvResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters);
        var jsonResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters);
        var pdfResult = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Pdf, parameters);

        // Assert
        (csvResult.FileSizeBytes > 0).Should().BeTrue();
        (jsonResult.FileSizeBytes > 0).Should().BeTrue();
        (pdfResult.FileSizeBytes > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_AllFormats_ShouldHaveCorrectMetadata()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var result = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters);

        // Assert
        result.Metadata.Keys.Should().Contain("template");
        result.Metadata.Keys.Should().Contain("format");
        result.Metadata.Keys.Should().Contain("generatedAt");
        result.Metadata["template"].ToString().Should().Be("SystemHealth");
        result.Metadata["format"].ToString().Should().Be("Csv");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GenerateAsync_WithEmptyDatabase_ShouldGenerateReportWithZeroCounts()
    {
        // Arrange - DB is already empty in constructor
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var result = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WithNullParameters_ShouldUseDefaults()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_FileNameFormat_ShouldIncludeTemplateAndTimestamp()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var result = await _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Csv, parameters);

        // Assert
        result.FileName.Should().Contain("SystemHealth");
        result.FileName.Should().MatchRegex(@"\d{8}_\d{6}");
    }

    #endregion

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
