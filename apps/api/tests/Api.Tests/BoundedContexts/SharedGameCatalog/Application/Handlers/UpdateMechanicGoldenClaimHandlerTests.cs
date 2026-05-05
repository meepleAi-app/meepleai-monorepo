using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using IEmbeddingService = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IEmbeddingService;
using IKeywordExtractor = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IKeywordExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateMechanicGoldenClaimHandlerTests
{
    private readonly Mock<IMechanicGoldenClaimRepository> _repositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IKeywordExtractor> _keywordsMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateMechanicGoldenClaimHandler>> _loggerMock;
    private readonly UpdateMechanicGoldenClaimHandler _handler;

    public UpdateMechanicGoldenClaimHandlerTests()
    {
        _repositoryMock = new Mock<IMechanicGoldenClaimRepository>();
        _embeddingMock = new Mock<IEmbeddingService>();
        _keywordsMock = new Mock<IKeywordExtractor>();
        _cacheMock = new Mock<IHybridCacheService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateMechanicGoldenClaimHandler>>();

        _handler = new UpdateMechanicGoldenClaimHandler(
            _repositoryMock.Object,
            _embeddingMock.Object,
            _keywordsMock.Object,
            _cacheMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Builds a real <see cref="MechanicGoldenClaim"/> aggregate via the public async factory using
    /// the supplied mocks. Production handler mutation semantics depend on <see cref="MechanicGoldenClaim.UpdateAsync"/>
    /// so we can't stub the aggregate itself — we seed it with deterministic embedding/keywords.
    /// </summary>
    private async Task<MechanicGoldenClaim> CreateSeedClaimAsync(Guid sharedGameId)
    {
        var seedEmbedding = new Mock<IEmbeddingService>();
        seedEmbedding
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.0f, 0.0f });
        var seedKeywords = new Mock<IKeywordExtractor>();
        seedKeywords
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "seed" });

        return await MechanicGoldenClaim.CreateAsync(
            sharedGameId: sharedGameId,
            section: MechanicSection.Victory,
            statement: "Original statement about victory conditions.",
            expectedPage: 3,
            sourceQuote: "Original source quote.",
            curatorUserId: Guid.NewGuid(),
            embedding: seedEmbedding.Object,
            keywords: seedKeywords.Object,
            ct: CancellationToken.None);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesClaimAndInvalidatesCache()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var claim = await CreateSeedClaimAsync(sharedGameId);
        var originalCreatedAt = claim.CreatedAt;

        var newEmbedding = new float[] { 0.5f, 0.6f, 0.7f };
        var newKeywords = new[] { "updated", "victory", "points" };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(claim.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        _embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newEmbedding);
        _keywordsMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(newKeywords);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new UpdateMechanicGoldenClaimCommand(
            ClaimId: claim.Id,
            Statement: "Updated statement: the game ends when a player reaches 15 victory points.",
            ExpectedPage: 9,
            SourceQuote: "When a player scores 15 or more victory points the round ends.");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        claim.Statement.Should().Be(command.Statement);
        claim.ExpectedPage.Should().Be(9);
        claim.SourceQuote.Should().Be(command.SourceQuote);
        claim.Keywords.Should().BeEquivalentTo(newKeywords);
        claim.Embedding.Should().BeEquivalentTo(newEmbedding);
        // Section is immutable; no way to change it through this command.
        claim.Section.Should().Be(MechanicSection.Victory);
        claim.CreatedAt.Should().Be(originalCreatedAt);
        claim.UpdatedAt.Should().BeOnOrAfter(originalCreatedAt);

        _embeddingMock.Verify(e => e.EmbedAsync(command.Statement, It.IsAny<CancellationToken>()), Times.Once);
        _keywordsMock.Verify(k => k.Extract(command.Statement), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(claim, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync($"golden:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ClaimNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(claimId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicGoldenClaim?)null);

        var command = new UpdateMechanicGoldenClaimCommand(
            ClaimId: claimId,
            Statement: "Anything.",
            ExpectedPage: 1,
            SourceQuote: "Anything.");

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.ResourceType.Should().Be("MechanicGoldenClaim");
        thrown.Which.ResourceId.Should().Be(claimId.ToString());

        // Must not persist nor invalidate cache when the claim doesn't exist.
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeactivatedClaim_ThrowsConflictException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var claim = await CreateSeedClaimAsync(sharedGameId);
        claim.Deactivate();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(claim.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        var command = new UpdateMechanicGoldenClaimCommand(
            ClaimId: claim.Id,
            Statement: "Trying to update a deactivated claim.",
            ExpectedPage: 2,
            SourceQuote: "Some quote.");

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Be("Cannot update a deactivated claim.");
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>();

        // Must not persist nor invalidate cache when the update is rejected.
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PersistsBeforeCacheInvalidation()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var claim = await CreateSeedClaimAsync(sharedGameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(claim.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        _embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });
        _keywordsMock
            .Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "k" });

        var sequence = new List<string>();
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("update"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("save"))
            .ReturnsAsync(1);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("cache"))
            .Returns(Task.CompletedTask);

        var command = new UpdateMechanicGoldenClaimCommand(
            ClaimId: claim.Id,
            Statement: "Updated statement.",
            ExpectedPage: 1,
            SourceQuote: "Updated quote.");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — persistence must happen before cache invalidation so readers don't repopulate
        // the cache from stale DB state.
        sequence.Should().Equal("update", "save", "cache");
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
        var act = () => new UpdateMechanicGoldenClaimHandler(
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
