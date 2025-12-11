using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for ReportGeneratorService
/// ISSUE-916: Report generation testing
/// </summary>
public sealed class ReportGeneratorServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ReportGeneratorService _sut;
    private readonly Mock<ILogger<ReportGeneratorService>> _loggerMock;

    public ReportGeneratorServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ReportTests_{Guid.NewGuid()}")
            .Options;

        var mediatorMock = new Mock<MediatR.IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        _loggerMock = new Mock<ILogger<ReportGeneratorService>>();
        _sut = new ReportGeneratorService(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateAsync_SystemHealth_ShouldGenerateCsvReport()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 24 };

        // Act
        var result = await _sut.GenerateAsync(
            ReportTemplate.SystemHealth,
            ReportFormat.Csv,
            parameters);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        Assert.EndsWith(".csv", result.FileName);
        Assert.Equal(result.Content.Length, result.FileSizeBytes);
        Assert.Contains("template", result.Metadata.Keys);
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidHours_ShouldThrowArgumentException()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = -1 };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GenerateAsync(ReportTemplate.SystemHealth, ReportFormat.Json, parameters));
    }

    [Fact]
    public async Task ValidateParameters_SystemHealth_ValidHours_ShouldReturnValid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 48 };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task ValidateParameters_SystemHealth_InvalidHours_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["hours"] = 1000 };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.SystemHealth, parameters);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("hours", errorMessage);
    }

    [Fact]
    public async Task ValidateParameters_UserActivity_MissingStartDate_ShouldReturnInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["endDate"] = DateTime.UtcNow };

        // Act
        var (isValid, errorMessage) = _sut.ValidateParameters(ReportTemplate.UserActivity, parameters);

        // Assert
        Assert.False(isValid);
        Assert.Contains("startDate", errorMessage!);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
