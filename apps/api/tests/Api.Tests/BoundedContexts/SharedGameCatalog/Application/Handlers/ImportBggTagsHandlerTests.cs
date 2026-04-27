using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ImportBggTagsHandlerTests
{
    private readonly Mock<IMechanicGoldenBggTagRepository> _repositoryMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ImportBggTagsHandler>> _loggerMock;
    private readonly ImportBggTagsHandler _handler;

    public ImportBggTagsHandlerTests()
    {
        _repositoryMock = new Mock<IMechanicGoldenBggTagRepository>();
        _cacheMock = new Mock<IHybridCacheService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ImportBggTagsHandler>>();

        _handler = new ImportBggTagsHandler(
            _repositoryMock.Object,
            _cacheMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_HappyPath_UpsertsBatchAndInvalidatesCache()
    {
        // Arrange — all 3 tags are new; repository reports 3 inserted.
        var sharedGameId = Guid.NewGuid();
        var tags = new List<BggTagDto>
        {
            new("CatanTag1", "cat1"),
            new("CatanTag2", "cat2"),
            new("CatanTag3", "cat3"),
        };

        _repositoryMock
            .Setup(r => r.UpsertBatchAsync(
                sharedGameId,
                It.IsAny<IReadOnlyList<(string Name, string Category)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new ImportBggTagsCommand(sharedGameId, tags);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — Inserted matches repo report; Skipped is requested - inserted.
        result.Inserted.Should().Be(3);
        result.Skipped.Should().Be(0);

        _repositoryMock.Verify(r => r.UpsertBatchAsync(
            sharedGameId,
            It.Is<IReadOnlyList<(string Name, string Category)>>(list =>
                list.Count == 3
                && list[0].Name == "CatanTag1" && list[0].Category == "cat1"
                && list[1].Name == "CatanTag2" && list[1].Category == "cat2"
                && list[2].Name == "CatanTag3" && list[2].Category == "cat3"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync($"golden:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PartialDuplicates_SplitsInsertedAndSkipped()
    {
        // Arrange — Sprint 2 / Task 17 contract: the importer UI needs to render
        // "Imported N (M skipped as duplicates)". Repo reports 2 inserted out of 4
        // submitted; handler computes Skipped = 4 - 2 = 2.
        var sharedGameId = Guid.NewGuid();
        var tags = new List<BggTagDto>
        {
            new("Area Control", "mechanic"),
            new("Set Collection", "mechanic"),
            new("Area Control", "mechanic"),       // in-batch duplicate
            new("Worker Placement", "mechanic"),   // already in DB
        };

        _repositoryMock
            .Setup(r => r.UpsertBatchAsync(
                sharedGameId,
                It.IsAny<IReadOnlyList<(string Name, string Category)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new ImportBggTagsCommand(sharedGameId, tags);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Inserted.Should().Be(2);
        result.Skipped.Should().Be(2, "Inserted + Skipped must equal Tags.Count");
    }

    [Fact]
    public async Task Handle_EmptyTagList_ReturnsZeroAndSkipsPersistence()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var command = new ImportBggTagsCommand(sharedGameId, new List<BggTagDto>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Inserted.Should().Be(0);
        result.Skipped.Should().Be(0);

        _repositoryMock.Verify(r => r.UpsertBatchAsync(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyList<(string, string)>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PersistsBeforeCacheInvalidation()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var tags = new List<BggTagDto>
        {
            new("Worker Placement", "mechanic"),
        };

        var sequence = new List<string>();
        _repositoryMock
            .Setup(r => r.UpsertBatchAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("upsert"))
            .ReturnsAsync(1);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("save"))
            .ReturnsAsync(1);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("cache"))
            .Returns(Task.CompletedTask);

        var command = new ImportBggTagsCommand(sharedGameId, tags);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — persistence must precede cache invalidation so readers never repopulate the
        // cache from stale DB state (same invariant enforced by the other golden-claim handlers).
        sequence.Should().Equal("upsert", "save", "cache");
    }

    [Fact]
    public async Task Handle_DuplicateNamesInInput_PassesAllToRepo()
    {
        // Arrange — dedupe is the repository's responsibility (see MechanicGoldenBggTagRepository).
        // The handler must be a transparent pass-through and forward every submitted tuple.
        var sharedGameId = Guid.NewGuid();
        var tags = new List<BggTagDto>
        {
            new("Area Control", "mechanic"),
            new("Area Control", "mechanic"), // duplicate
            new("Set Collection", "mechanic"),
        };

        _repositoryMock
            .Setup(r => r.UpsertBatchAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new ImportBggTagsCommand(sharedGameId, tags);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — repo receives all 3 tuples (dedupe happens inside the repo).
        // 2 of 3 went in, so the in-batch duplicate is reported as Skipped=1.
        result.Inserted.Should().Be(2);
        result.Skipped.Should().Be(1);
        _repositoryMock.Verify(r => r.UpsertBatchAsync(
            sharedGameId,
            It.Is<IReadOnlyList<(string Name, string Category)>>(list => list.Count == 3),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        var act = () => new ImportBggTagsHandler(
            repository: null!,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new ImportBggTagsHandler(
            repository: _repositoryMock.Object,
            cache: null!,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new ImportBggTagsHandler(
            repository: _repositoryMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: null!,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new ImportBggTagsHandler(
            repository: _repositoryMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_WithNullCommand_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("command");
    }
}
