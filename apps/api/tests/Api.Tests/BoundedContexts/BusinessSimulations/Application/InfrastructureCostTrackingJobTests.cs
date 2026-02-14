using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Application.Jobs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application;

/// <summary>
/// Unit tests for InfrastructureCostTrackingJob (Issue #3721)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class InfrastructureCostTrackingJobTests
{
    private readonly Mock<ILedgerTrackingService> _ledgerTrackingServiceMock;
    private readonly Mock<ILlmCostLogRepository> _costLogRepositoryMock;
    private readonly Mock<ILogger<InfrastructureCostTrackingJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _jobContextMock;
    private readonly InfrastructureCostTrackingJob _job;

    public InfrastructureCostTrackingJobTests()
    {
        _ledgerTrackingServiceMock = new Mock<ILedgerTrackingService>();
        _costLogRepositoryMock = new Mock<ILlmCostLogRepository>();
        _loggerMock = new Mock<ILogger<InfrastructureCostTrackingJob>>();
        _jobContextMock = new Mock<IJobExecutionContext>();

        _jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _job = new InfrastructureCostTrackingJob(
            _ledgerTrackingServiceMock.Object,
            _costLogRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_WithDailyCosts_ShouldCreateLedgerEntry()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25.50m);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(yesterday, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { { "openai", 15.30m }, { "anthropic", 10.20m } });

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackInfrastructureCostAsync(
                25.50m,
                It.Is<string>(d => d.Contains(yesterday.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))),
                It.Is<string>(m => m.Contains("openai") && m.Contains("anthropic")),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithZeroCosts_ShouldSkipLedgerEntry()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackInfrastructureCostAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_WithNegativeCosts_ShouldSkipLedgerEntry()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1m);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackInfrastructureCostAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldSetSuccessResult_WhenCostsExist()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(yesterday, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { { "openai", 10m } });

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r => r != null), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldSetSkippedResult_WhenNoCosts()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r => r != null), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenRepositoryThrows_ShouldThrowJobExecutionException()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        // Act
        var act = () => _job.Execute(_jobContextMock.Object);

        // Assert
        await act.Should().ThrowAsync<JobExecutionException>()
            .Where(ex => ex.InnerException is InvalidOperationException);
    }

    [Fact]
    public async Task Execute_WhenLedgerServiceThrows_ShouldThrowJobExecutionException()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(yesterday, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { { "openai", 10m } });

        _ledgerTrackingServiceMock
            .Setup(s => s.TrackInfrastructureCostAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Ledger error"));

        // Act
        var act = () => _job.Execute(_jobContextMock.Object);

        // Assert
        await act.Should().ThrowAsync<JobExecutionException>();
    }

    [Fact]
    public async Task Execute_MetadataContainsProviderBreakdown()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m);

        var providers = new Dictionary<string, decimal>
        {
            { "openai", 30m },
            { "anthropic", 15m },
            { "google", 5m }
        };
        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(yesterday, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);

        string? capturedMetadata = null;
        _ledgerTrackingServiceMock
            .Setup(s => s.TrackInfrastructureCostAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<decimal, string, string?, DateTime?, CancellationToken>(
                (_, _, metadata, _, _) => capturedMetadata = metadata)
            .Returns(Task.CompletedTask);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        capturedMetadata.Should().NotBeNull();
        capturedMetadata.Should().Contain("openai");
        capturedMetadata.Should().Contain("anthropic");
        capturedMetadata.Should().Contain("google");
        capturedMetadata.Should().Contain("50");
    }

    [Fact]
    public async Task Execute_UsesYesterdayDate()
    {
        // Arrange
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(yesterday, yesterday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { { "openai", 10m } });

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert - verify it queries for yesterday
        _costLogRepositoryMock.Verify(
            r => r.GetDailyCostAsync(yesterday, It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_WithNullLedgerService_ShouldThrow()
    {
        var act = () => new InfrastructureCostTrackingJob(null!, _costLogRepositoryMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ledgerTrackingService");
    }

    [Fact]
    public void Constructor_WithNullCostLogRepository_ShouldThrow()
    {
        var act = () => new InfrastructureCostTrackingJob(_ledgerTrackingServiceMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("costLogRepository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new InfrastructureCostTrackingJob(_ledgerTrackingServiceMock.Object, _costLogRepositoryMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    [Fact]
    public void Job_ShouldHaveDisallowConcurrentExecutionAttribute()
    {
        // Assert
        var attribute = typeof(InfrastructureCostTrackingJob)
            .GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), false);
        attribute.Should().HaveCount(1);
    }
}
