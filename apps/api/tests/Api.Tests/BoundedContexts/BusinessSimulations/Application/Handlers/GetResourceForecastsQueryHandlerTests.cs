using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for GetResourceForecastsQueryHandler (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetResourceForecastsQueryHandlerTests
{
    private readonly Mock<IResourceForecastRepository> _repositoryMock;
    private readonly Mock<ILogger<GetResourceForecastsQueryHandler>> _loggerMock;
    private readonly GetResourceForecastsQueryHandler _handler;

    public GetResourceForecastsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IResourceForecastRepository>();
        _loggerMock = new Mock<ILogger<GetResourceForecastsQueryHandler>>();
        _handler = new GetResourceForecastsQueryHandler(
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingForecasts_ShouldReturnPaginatedResponse()
    {
        var userId = Guid.NewGuid();
        var forecasts = new List<ResourceForecast>
        {
            CreateForecast("Forecast 1", userId),
            CreateForecast("Forecast 2", userId),
        };

        _repositoryMock
            .Setup(r => r.GetByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((forecasts, 2));

        var query = new GetResourceForecastsQuery(userId, Page: 1, PageSize: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithNoForecasts_ShouldReturnEmptyResponse()
    {
        var userId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ResourceForecast>(), 0));

        var query = new GetResourceForecastsQuery(userId, Page: 1, PageSize: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldMapEntityPropertiesToDto()
    {
        var userId = Guid.NewGuid();
        var forecast = CreateForecast("Test Forecast", userId);
        var forecasts = new List<ResourceForecast> { forecast };

        _repositoryMock
            .Setup(r => r.GetByUserAsync(userId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((forecasts, 1));

        var query = new GetResourceForecastsQuery(userId, Page: 1, PageSize: 10);

        var result = await _handler.Handle(query, CancellationToken.None);

        var dto = result.Items[0];
        dto.Name.Should().Be("Test Forecast");
        dto.GrowthPattern.Should().Be("Linear");
        dto.MonthlyGrowthRate.Should().Be(10m);
        dto.CurrentUsers.Should().Be(1000);
        dto.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ShouldThrowArgumentNullException()
    {
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        var act = () => new GetResourceForecastsQueryHandler(null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new GetResourceForecastsQueryHandler(_repositoryMock.Object, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ResourceForecast CreateForecast(string name, Guid userId) =>
        ResourceForecast.Create(
            name: name, growthPattern: "Linear",
            monthlyGrowthRate: 10m, currentUsers: 1000,
            currentDbSizeGb: 5m, currentDailyTokens: 500_000,
            currentCacheMb: 256m, currentVectorEntries: 100_000,
            dbPerUserMb: 2m, tokensPerUserPerDay: 500,
            cachePerUserMb: 0.5m, vectorsPerUser: 100,
            projectionsJson: "[{\"month\":1}]", recommendationsJson: null,
            projectedMonthlyCost: 150m, createdByUserId: userId);
}
