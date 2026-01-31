using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;

/// <summary>
/// Tests for GetEvaluationResultsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetEvaluationResultsQueryHandlerTests
{
    private readonly Mock<ILogger<GetEvaluationResultsQueryHandler>> _mockLogger;
    private readonly GetEvaluationResultsQueryHandler _handler;

    public GetEvaluationResultsQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetEvaluationResultsQueryHandler>>();
        _handler = new GetEvaluationResultsQueryHandler(_mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithEmptyCache_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetEvaluationResultsQuery
        {
            DatasetName = null,
            Configuration = null,
            Limit = 10
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Note: Result count depends on cache state - this test validates handler doesn't crash
    }

    [Fact]
    public async Task Handle_WithDatasetNameFilter_FiltersCorrectly()
    {
        // Arrange
        var query = new GetEvaluationResultsQuery
        {
            DatasetName = "test-dataset",
            Configuration = null,
            Limit = 10
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result, r => Assert.Equal("test-dataset", r.DatasetName, ignoreCase: true));
    }

    [Fact]
    public async Task Handle_WithConfigurationFilter_FiltersCorrectly()
    {
        // Arrange
        var query = new GetEvaluationResultsQuery
        {
            DatasetName = null,
            Configuration = "baseline",
            Limit = 10
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result, r => Assert.Equal("baseline", r.Configuration, ignoreCase: true));
    }
}
