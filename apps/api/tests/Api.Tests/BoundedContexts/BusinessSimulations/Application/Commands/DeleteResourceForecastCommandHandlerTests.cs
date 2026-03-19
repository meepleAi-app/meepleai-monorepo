using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Unit tests for DeleteResourceForecastCommandHandler (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class DeleteResourceForecastCommandHandlerTests
{
    private readonly Mock<IResourceForecastRepository> _repositoryMock;
    private readonly Mock<ILogger<DeleteResourceForecastCommandHandler>> _loggerMock;
    private readonly DeleteResourceForecastCommandHandler _handler;

    public DeleteResourceForecastCommandHandlerTests()
    {
        _repositoryMock = new Mock<IResourceForecastRepository>();
        _loggerMock = new Mock<ILogger<DeleteResourceForecastCommandHandler>>();
        _handler = new DeleteResourceForecastCommandHandler(
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingForecast_ShouldDeleteSuccessfully()
    {
        var forecastId = Guid.NewGuid();
        var forecast = ResourceForecast.Create(
            name: "Test", growthPattern: "Linear",
            monthlyGrowthRate: 10m, currentUsers: 1000,
            currentDbSizeGb: 5m, currentDailyTokens: 500_000,
            currentCacheMb: 256m, currentVectorEntries: 100_000,
            dbPerUserMb: 2m, tokensPerUserPerDay: 500,
            cachePerUserMb: 0.5m, vectorsPerUser: 100,
            projectionsJson: "[]", recommendationsJson: null,
            projectedMonthlyCost: 100m, createdByUserId: Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(forecastId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _repositoryMock
            .Setup(r => r.DeleteAsync(forecast, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteResourceForecastCommand(forecastId);

        await _handler.Handle(command, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.DeleteAsync(forecast, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentForecast_ShouldThrowNotFoundException()
    {
        var forecastId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(forecastId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceForecast?)null);

        var command = new DeleteResourceForecastCommand(forecastId);
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        var act = () => new DeleteResourceForecastCommandHandler(null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new DeleteResourceForecastCommandHandler(_repositoryMock.Object, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
