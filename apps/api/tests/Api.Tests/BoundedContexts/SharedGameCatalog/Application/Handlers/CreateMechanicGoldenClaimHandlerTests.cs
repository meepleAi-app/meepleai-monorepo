using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using IEmbeddingService = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IEmbeddingService;
using IKeywordExtractor = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IKeywordExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CreateMechanicGoldenClaimHandlerTests
{
    private readonly Mock<IMechanicGoldenClaimRepository> _repositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IKeywordExtractor> _keywordsMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateMechanicGoldenClaimHandler>> _loggerMock;
    private readonly CreateMechanicGoldenClaimHandler _handler;

    public CreateMechanicGoldenClaimHandlerTests()
    {
        _repositoryMock = new Mock<IMechanicGoldenClaimRepository>();
        _embeddingMock = new Mock<IEmbeddingService>();
        _keywordsMock = new Mock<IKeywordExtractor>();
        _cacheMock = new Mock<IHybridCacheService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateMechanicGoldenClaimHandler>>();

        _handler = new CreateMechanicGoldenClaimHandler(
            _repositoryMock.Object,
            _embeddingMock.Object,
            _keywordsMock.Object,
            _cacheMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesClaimAndInvalidatesCache()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var curatorId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var keywords = new[] { "victory", "points", "end" };

        _embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _keywordsMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(keywords);

        MechanicGoldenClaim? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()))
            .Callback<MechanicGoldenClaim, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        var command = new CreateMechanicGoldenClaimCommand(
            SharedGameId: sharedGameId,
            Section: MechanicSection.Victory,
            Statement: "The game ends when a player reaches 10 victory points.",
            ExpectedPage: 7,
            SourceQuote: "When a player scores 10 or more victory points, the game immediately ends.",
            CuratorUserId: curatorId);

        // Act
        var id = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        id.Should().NotBe(Guid.Empty);
        captured.Should().NotBeNull();
        captured!.Id.Should().Be(id);
        captured.SharedGameId.Should().Be(sharedGameId);
        captured.Section.Should().Be(MechanicSection.Victory);
        captured.Statement.Should().Be(command.Statement);
        captured.ExpectedPage.Should().Be(7);
        captured.SourceQuote.Should().Be(command.SourceQuote);
        captured.CuratorUserId.Should().Be(curatorId);
        captured.Keywords.Should().BeEquivalentTo(keywords);
        captured.Embedding.Should().BeEquivalentTo(embedding);

        _embeddingMock.Verify(e => e.EmbedAsync(command.Statement, It.IsAny<CancellationToken>()), Times.Once);
        _keywordsMock.Verify(k => k.Extract(command.Statement), Times.Once);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync($"golden:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PersistsBeforeCacheInvalidation()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        _embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });
        _keywordsMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "k" });

        var sequence = new List<string>();
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("add"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("save"))
            .ReturnsAsync(1);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("cache"))
            .Returns(Task.CompletedTask);

        var command = new CreateMechanicGoldenClaimCommand(
            SharedGameId: sharedGameId,
            Section: MechanicSection.Mechanics,
            Statement: "A statement.",
            ExpectedPage: 1,
            SourceQuote: "A quote.",
            CuratorUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — persistence must happen before cache invalidation so readers don't repopulate
        // the cache from stale DB state.
        sequence.Should().Equal("add", "save", "cache");
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: null!,
            embedding: _embeddingMock.Object,
            keywords: _keywordsMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullEmbedding_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            embedding: null!,
            keywords: _keywordsMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("embedding");
    }

    [Fact]
    public void Constructor_WithNullKeywords_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            embedding: _embeddingMock.Object,
            keywords: null!,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("keywords");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            embedding: _embeddingMock.Object,
            keywords: _keywordsMock.Object,
            cache: null!,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            embedding: _embeddingMock.Object,
            keywords: _keywordsMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: null!,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new CreateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            embedding: _embeddingMock.Object,
            keywords: _keywordsMock.Object,
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
