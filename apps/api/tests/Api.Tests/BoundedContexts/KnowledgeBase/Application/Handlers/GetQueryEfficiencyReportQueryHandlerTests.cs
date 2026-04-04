using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetQueryEfficiencyReportQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetQueryEfficiencyReportQueryHandlerTests
{
    private readonly Mock<IQueryEfficiencyAnalyzer> _mockAnalyzer;
    private readonly Mock<ILogger<GetQueryEfficiencyReportQueryHandler>> _mockLogger;
    private readonly GetQueryEfficiencyReportQueryHandler _handler;

    public GetQueryEfficiencyReportQueryHandlerTests()
    {
        _mockAnalyzer = new Mock<IQueryEfficiencyAnalyzer>();
        _mockLogger = new Mock<ILogger<GetQueryEfficiencyReportQueryHandler>>();
        _handler = new GetQueryEfficiencyReportQueryHandler(_mockAnalyzer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsDto()
    {
        // Arrange
        var startDate = new DateOnly(2024, 6, 1);
        var endDate = new DateOnly(2024, 6, 30);

        var domainReport = new QueryEfficiencyReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalQueries = 500,
            TotalCost = 75.25m,
            TotalTokens = 250000,
            AverageTokensPerQuery = 500,
            AverageCostPerQuery = 0.15m,
            TopCostlyQueries = new List<QueryTypeCost>
            {
                new()
                {
                    QueryType = "QA",
                    QueryCount = 200,
                    TotalCost = 40.00m,
                    TotalTokens = 120000,
                    AverageTokens = 600,
                    AverageCost = 0.20m
                }
            },
            AverageTokensByOperation = new Dictionary<string, double>
            {
                { "QA", 600 },
                { "Explain", 400 }
            },
            OptimizationRecommendations = new List<string>
            {
                "Consider reducing context size for QA queries",
                "Use caching for repeated questions"
            }
        };

        _mockAnalyzer.Setup(a => a.AnalyzeEfficiencyAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainReport);

        var query = new GetQueryEfficiencyReportQuery { StartDate = startDate, EndDate = endDate };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
        result.TotalQueries.Should().Be(500);
        result.TotalCost.Should().Be(75.25m);
        result.TotalTokens.Should().Be(250000);
        result.AverageTokensPerQuery.Should().Be(500);
        result.AverageCostPerQuery.Should().Be(0.15m);
        result.TopCostlyQueries.Should().ContainSingle();
        result.TopCostlyQueries[0].QueryType.Should().Be("QA");
        result.AverageTokensByOperation.Count.Should().Be(2);
        result.OptimizationRecommendations.Count.Should().Be(2);

        _mockAnalyzer.Verify(a => a.AnalyzeEfficiencyAsync(startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyReport_ReturnsEmptyDto()
    {
        // Arrange
        var startDate = new DateOnly(2024, 6, 1);
        var endDate = new DateOnly(2024, 6, 30);

        var domainReport = new QueryEfficiencyReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalQueries = 0,
            TotalCost = 0,
            TotalTokens = 0,
            AverageTokensPerQuery = 0,
            AverageCostPerQuery = 0,
            TopCostlyQueries = new List<QueryTypeCost>(),
            AverageTokensByOperation = new Dictionary<string, double>(),
            OptimizationRecommendations = new List<string>()
        };

        _mockAnalyzer.Setup(a => a.AnalyzeEfficiencyAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainReport);

        var query = new GetQueryEfficiencyReportQuery { StartDate = startDate, EndDate = endDate };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalQueries.Should().Be(0);
        result.TotalCost.Should().Be(0);
        result.TopCostlyQueries.Should().BeEmpty();
        result.AverageTokensByOperation.Should().BeEmpty();
        result.OptimizationRecommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AnalyzerThrowsException_PropagatesException()
    {
        // Arrange
        var startDate = new DateOnly(2024, 6, 1);
        var endDate = new DateOnly(2024, 6, 30);

        _mockAnalyzer.Setup(a => a.AnalyzeEfficiencyAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Analysis failed"));

        var query = new GetQueryEfficiencyReportQuery { StartDate = startDate, EndDate = endDate };

        // Act & Assert
        Func<Task> act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Analysis failed");

        _mockAnalyzer.Verify(a => a.AnalyzeEfficiencyAsync(startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
