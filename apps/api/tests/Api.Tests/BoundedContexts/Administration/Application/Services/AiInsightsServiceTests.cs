using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using DomainInsightType = Api.BoundedContexts.Administration.Domain.ValueObjects.InsightType;
using DtoInsightType = Api.BoundedContexts.Administration.Application.DTOs.InsightType;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Unit tests for AiInsightsService (Issue #3916, refactored #4308).
/// Tests AI insights generation via IUserInsightsService domain service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class AiInsightsServiceTests
{
    private readonly Mock<IUserInsightsService> _mockUserInsightsService;
    private readonly Mock<ILogger<AiInsightsService>> _mockLogger;
    private readonly AiInsightsService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public AiInsightsServiceTests()
    {
        _mockUserInsightsService = new Mock<IUserInsightsService>();
        _mockLogger = new Mock<ILogger<AiInsightsService>>();

        _service = new AiInsightsService(
            _mockUserInsightsService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetInsightsAsync_WithDomainInsights_ReturnsMappedDtos()
    {
        // Arrange
        var domainInsights = new List<AIInsight>
        {
            AIInsight.Create(
                DomainInsightType.Recommendation,
                "Spirit Island",
                "Based on your recent games",
                "Scopri \u2192",
                "/library/games/spirit-island",
                9),
            AIInsight.Create(
                DomainInsightType.BacklogAlert,
                "Gloomhaven",
                "Unplayed for 30 days",
                "Gioca ora \u2192",
                "/library?filter=unplayed",
                7)
        };

        _mockUserInsightsService
            .Setup(s => s.GenerateInsightsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainInsights);

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        result.Should().NotBeNull();
        result.Insights.Count.Should().Be(2);
        (result.GeneratedAt <= DateTime.UtcNow).Should().BeTrue();
        (result.NextRefresh > result.GeneratedAt).Should().BeTrue();
    }

    [Fact]
    public async Task GetInsightsAsync_EmptyInsights_ReturnsEmptyList()
    {
        // Arrange
        _mockUserInsightsService
            .Setup(s => s.GenerateInsightsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        result.Should().NotBeNull();
        result.Insights.Should().BeEmpty();
        (result.GeneratedAt <= DateTime.UtcNow).Should().BeTrue();
        (result.NextRefresh > result.GeneratedAt).Should().BeTrue();
    }

    [Fact]
    public async Task GetInsightsAsync_ServiceThrows_ReturnsEmptyFallback()
    {
        // Arrange
        _mockUserInsightsService
            .Setup(s => s.GenerateInsightsAsync(_userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert - should gracefully handle errors
        result.Should().NotBeNull();
        result.Insights.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInsightsAsync_PerformanceUnder1Second()
    {
        // Arrange
        _mockUserInsightsService
            .Setup(s => s.GenerateInsightsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _service.GetInsightsAsync(_userId);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        (stopwatch.ElapsedMilliseconds < 1000).Should().BeTrue($"Expected <1000ms, got {stopwatch.ElapsedMilliseconds}ms");
    }
}
