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
public class DeactivateMechanicGoldenClaimHandlerTests
{
    private readonly Mock<IMechanicGoldenClaimRepository> _repositoryMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeactivateMechanicGoldenClaimHandler>> _loggerMock;
    private readonly DeactivateMechanicGoldenClaimHandler _handler;

    public DeactivateMechanicGoldenClaimHandlerTests()
    {
        _repositoryMock = new Mock<IMechanicGoldenClaimRepository>();
        _cacheMock = new Mock<IHybridCacheService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeactivateMechanicGoldenClaimHandler>>();

        _handler = new DeactivateMechanicGoldenClaimHandler(
            _repositoryMock.Object,
            _cacheMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Builds a real <see cref="MechanicGoldenClaim"/> aggregate via the public async factory using
    /// stubbed embedding/keyword services. The Deactivate handler does not itself depend on those
    /// services, but the aggregate factory does, so we still need them for seeding.
    /// </summary>
    private static async Task<MechanicGoldenClaim> CreateSeedClaimAsync(Guid sharedGameId)
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
    public async Task Handle_HappyPath_DeactivatesClaimAndInvalidatesCache()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var claim = await CreateSeedClaimAsync(sharedGameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(claim.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicGoldenClaim>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeactivateMechanicGoldenClaimCommand(ClaimId: claim.Id);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        claim.DeletedAt.Should().NotBeNull("the aggregate must record a soft-delete timestamp");
        claim.DeletedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

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

        var command = new DeactivateMechanicGoldenClaimCommand(ClaimId: claimId);

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
    public async Task Handle_AlreadyDeactivatedClaim_ThrowsConflictException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var claim = await CreateSeedClaimAsync(sharedGameId);
        claim.Deactivate(); // First deactivation happens in arrange — handler must reject the second.

        _repositoryMock
            .Setup(r => r.GetByIdAsync(claim.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        var command = new DeactivateMechanicGoldenClaimCommand(ClaimId: claim.Id);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Be("Already deactivated.");
        thrown.Which.InnerException.Should().BeOfType<InvalidOperationException>();

        // Must not persist nor invalidate cache when the deactivation is rejected.
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

        var command = new DeactivateMechanicGoldenClaimCommand(ClaimId: claim.Id);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — persistence must happen before cache invalidation so readers don't repopulate
        // the cache from stale DB state.
        sequence.Should().Equal("update", "save", "cache");
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        var act = () => new DeactivateMechanicGoldenClaimHandler(
            repository: null!,
            cache: _cacheMock.Object,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new DeactivateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            cache: null!,
            unitOfWork: _unitOfWorkMock.Object,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new DeactivateMechanicGoldenClaimHandler(
            repository: _repositoryMock.Object,
            cache: _cacheMock.Object,
            unitOfWork: null!,
            logger: _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new DeactivateMechanicGoldenClaimHandler(
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
