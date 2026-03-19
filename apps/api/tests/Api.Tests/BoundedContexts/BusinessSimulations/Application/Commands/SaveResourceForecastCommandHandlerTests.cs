using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Unit tests for SaveResourceForecastCommandHandler (Issue #3726)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class SaveResourceForecastCommandHandlerTests
{
    private readonly Mock<IResourceForecastRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SaveResourceForecastCommandHandler>> _loggerMock;
    private readonly SaveResourceForecastCommandHandler _handler;

    public SaveResourceForecastCommandHandlerTests()
    {
        _repositoryMock = new Mock<IResourceForecastRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SaveResourceForecastCommandHandler>>();
        _handler = new SaveResourceForecastCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSaveAndReturnId()
    {
        var command = CreateValidCommand();

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ResourceForecast>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ResourceForecast>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateEntityWithCorrectProperties()
    {
        ResourceForecast? capturedEntity = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ResourceForecast>(), It.IsAny<CancellationToken>()))
            .Callback<ResourceForecast, CancellationToken>((entity, _) => capturedEntity = entity)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = CreateValidCommand();

        await _handler.Handle(command, CancellationToken.None);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Name.Should().Be(command.Name);
        capturedEntity.GrowthPattern.Should().Be(command.GrowthPattern);
        capturedEntity.MonthlyGrowthRate.Should().Be(command.MonthlyGrowthRate);
        capturedEntity.CurrentUsers.Should().Be(command.CurrentUsers);
        capturedEntity.ProjectionsJson.Should().Be(command.ProjectionsJson);
        capturedEntity.CreatedByUserId.Should().Be(command.CreatedByUserId);
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
        var act = () => new SaveResourceForecastCommandHandler(
            null!, _unitOfWorkMock.Object, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ShouldThrowArgumentNullException()
    {
        var act = () => new SaveResourceForecastCommandHandler(
            _repositoryMock.Object, null!, _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var act = () => new SaveResourceForecastCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static SaveResourceForecastCommand CreateValidCommand() => new(
        Name: "Test Forecast",
        GrowthPattern: "Linear",
        MonthlyGrowthRate: 10m,
        CurrentUsers: 1000,
        CurrentDbSizeGb: 5m,
        CurrentDailyTokens: 500_000,
        CurrentCacheMb: 256m,
        CurrentVectorEntries: 100_000,
        DbPerUserMb: 2m,
        TokensPerUserPerDay: 500,
        CachePerUserMb: 0.5m,
        VectorsPerUser: 100,
        ProjectionsJson: "[{\"month\":1}]",
        RecommendationsJson: null,
        ProjectedMonthlyCost: 150m,
        CreatedByUserId: Guid.NewGuid());
}
