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
/// Tests for GetMonthlyOptimizationReportQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetMonthlyOptimizationReportQueryHandlerTests
{
    private readonly Mock<IMonthlyOptimizationReportService> _mockReportService;
    private readonly Mock<ILogger<GetMonthlyOptimizationReportQueryHandler>> _mockLogger;
    private readonly GetMonthlyOptimizationReportQueryHandler _handler;

    public GetMonthlyOptimizationReportQueryHandlerTests()
    {
        _mockReportService = new Mock<IMonthlyOptimizationReportService>();
        _mockLogger = new Mock<ILogger<GetMonthlyOptimizationReportQueryHandler>>();
        _handler = new GetMonthlyOptimizationReportQueryHandler(_mockReportService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsReport()
    {
        // Arrange
        var year = 2024;
        var month = 6;

        var expectedReport = new MonthlyOptimizationReport
        {
            Year = year,
            Month = month,
            EfficiencyAnalysis = new QueryEfficiencyReport
            {
                TotalCost = 150.50m,
                TotalQueries = 1000,
                TotalTokens = 500000,
                AverageTokensPerQuery = 500,
                AverageCostPerQuery = 0.15m,
                StartDate = new DateOnly(year, month, 1),
                EndDate = new DateOnly(year, month, 30),
                TopCostlyQueries = new List<QueryTypeCost>(),
                AverageTokensByOperation = new Dictionary<string, double>(),
                OptimizationRecommendations = new List<string>()
            },
            TotalSavingsOpportunity = 25.75m,
            CacheAnalysis = null!,
            ModelComparisons = new List<ModelComparison>(),
            RecommendedModel = null!,
            ExecutiveSummary = new List<string>()
        };

        _mockReportService.Setup(s => s.GenerateReportAsync(year, month, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var query = new GetMonthlyOptimizationReportQuery { Year = year, Month = month };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Year.Should().Be(year);
        result.Month.Should().Be(month);
        result.EfficiencyAnalysis.TotalCost.Should().Be(150.50m);
        result.TotalSavingsOpportunity.Should().Be(25.75m);

        _mockReportService.Verify(s => s.GenerateReportAsync(year, month, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceThrowsException_PropagatesException()
    {
        // Arrange
        var year = 2024;
        var month = 6;

        _mockReportService.Setup(s => s.GenerateReportAsync(year, month, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Report generation failed"));

        var query = new GetMonthlyOptimizationReportQuery { Year = year, Month = month };

        // Act & Assert
        Func<Task> act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Report generation failed");

        _mockReportService.Verify(s => s.GenerateReportAsync(year, month, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
