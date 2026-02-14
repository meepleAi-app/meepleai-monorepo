using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
public class CalculateTrendingJobTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<CalculateTrendingJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _jobContextMock;
    private readonly CalculateTrendingJob _job;

    public CalculateTrendingJobTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<CalculateTrendingJob>>();
        _jobContextMock = new Mock<IJobExecutionContext>();
        _jobContextMock.Setup(c => c.CancellationToken).Returns(TestContext.Current.CancellationToken);
        _jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _job = new CalculateTrendingJob(
            _mediatorMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_InvalidatesCache_ThenQueriesTrending()
    {
        // Arrange
        var trendingResult = new List<TrendingGameDto>
        {
            new() { Rank = 1, GameId = Guid.NewGuid(), Title = "Top Game", Score = 42.5 },
            new() { Rank = 2, GameId = Guid.NewGuid(), Title = "Second Game", Score = 30.0 }
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCatalogTrendingQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendingResult);

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(
            "catalog:trending",
            It.IsAny<CancellationToken>()), Times.Once);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GetCatalogTrendingQuery>(q => q.Limit == 10),
            It.IsAny<CancellationToken>()), Times.Once);

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("GamesComputed")!.GetValue(r)!.Equals(2)));
    }

    [Fact]
    public async Task Execute_WithNoTrendingGames_SetsZeroTopScore()
    {
        // Arrange
        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCatalogTrendingQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrendingGameDto>());

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(
            "catalog:trending",
            It.IsAny<CancellationToken>()), Times.Once);

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("GamesComputed")!.GetValue(r)!.Equals(0) &&
            r.GetType().GetProperty("TopScore")!.GetValue(r)!.Equals(0.0)));
    }

    [Fact]
    public async Task Execute_WhenMediatorThrows_CatchesAndSetsFailureResult()
    {
        // Arrange
        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCatalogTrendingQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert - job should NOT throw (Quartz pattern)
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false) &&
            r.GetType().GetProperty("Error")!.GetValue(r)!.ToString()!.Contains("Database connection failed")));
    }

    [Fact]
    public async Task Execute_WhenCacheRemoveFails_CatchesAndSetsFailureResult()
    {
        // Arrange
        _cacheMock.Setup(c => c.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert - job should NOT throw
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false)));

        // Mediator should NOT have been called since cache invalidation failed first
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<GetCatalogTrendingQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_CacheInvalidationHappensBeforeQuery()
    {
        // Arrange
        var callOrder = new List<string>();

        _cacheMock.Setup(c => c.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("cache_remove"))
            .Returns(Task.CompletedTask);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCatalogTrendingQuery>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("mediator_send"))
            .ReturnsAsync(new List<TrendingGameDto>());

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        callOrder.Should().ContainInOrder("cache_remove", "mediator_send");
    }

    [Fact]
    public void Constructor_WithNullMediator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CalculateTrendingJob(null!, _cacheMock.Object, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("mediator");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CalculateTrendingJob(_mediatorMock.Object, null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CalculateTrendingJob(_mediatorMock.Object, _cacheMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
