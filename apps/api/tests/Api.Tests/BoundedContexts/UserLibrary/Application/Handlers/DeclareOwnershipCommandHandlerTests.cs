using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for DeclareOwnershipCommandHandler.
/// Verifies ownership declaration, RAG access check, and idempotency.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class DeclareOwnershipCommandHandlerTests : IDisposable
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IRagAccessService> _mockRagAccessService = new();
    private readonly Mock<ILogger<DeclareOwnershipCommandHandler>> _mockLogger = new();
    private readonly Api.Infrastructure.MeepleAiDbContext _db;
    private readonly DeclareOwnershipCommandHandler _handler;

    public DeclareOwnershipCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();

        _handler = new DeclareOwnershipCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _mockRagAccessService.Object,
            _db,
            _mockLogger.Object);
    }

    public void Dispose() => _db.Dispose();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeclareOwnershipCommandHandler(
            null!,
            _mockUnitOfWork.Object,
            _mockRagAccessService.Object,
            _db,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("libraryRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeclareOwnershipCommandHandler(
            _mockLibraryRepo.Object,
            null!,
            _mockRagAccessService.Object,
            _db,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullRagAccessService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeclareOwnershipCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            null!,
            _db,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ragAccessService");
    }

    [Fact]
    public void Constructor_WithNullDb_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeclareOwnershipCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _mockRagAccessService.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("db");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeclareOwnershipCommandHandler(
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _mockRagAccessService.Object,
            _db,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WhenEntryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new DeclareOwnershipCommand(UserId: userId, GameId: gameId);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{gameId}*");
    }

    [Fact]
    public async Task Handle_WithValidEntry_DeclaresOwnershipAndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var command = new DeclareOwnershipCommand(UserId: userId, GameId: gameId);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockRagAccessService
            .Setup(s => s.CanAccessRagAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockRagAccessService
            .Setup(s => s.GetAccessibleKbCardsAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidEntry_ReturnsResultWithOwnershipInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var command = new DeclareOwnershipCommand(UserId: userId, GameId: gameId);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockRagAccessService
            .Setup(s => s.CanAccessRagAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockRagAccessService
            .Setup(s => s.GetAccessibleKbCardsAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasRagAccess.Should().BeTrue();
        result.KbCardCount.Should().Be(2);
        result.OwnershipDeclaredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenRagAccessIsDenied_ReturnsResultWithNoRagAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var command = new DeclareOwnershipCommand(UserId: userId, GameId: gameId);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockRagAccessService
            .Setup(s => s.CanAccessRagAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockRagAccessService
            .Setup(s => s.GetAccessibleKbCardsAsync(userId, gameId, Api.Infrastructure.Entities.UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.HasRagAccess.Should().BeFalse();
        result.KbCardCount.Should().Be(0);
    }

    #endregion
}
