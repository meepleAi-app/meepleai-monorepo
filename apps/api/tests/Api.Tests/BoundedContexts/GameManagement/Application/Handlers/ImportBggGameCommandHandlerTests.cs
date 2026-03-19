using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Unit tests for ImportBggGameCommandHandler.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ImportBggGameCommandHandlerTests
{
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly Mock<IBggApiClient> _bggApiClientMock;
    private readonly Mock<IPrivateGameRepository> _privateGameRepoMock;
    private readonly Mock<IUserLibraryRepository> _userLibraryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ImportBggGameCommandHandler>> _loggerMock;
    private readonly ImportBggGameCommandHandler _sut;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private const int TestBggId = 12345;

    public ImportBggGameCommandHandlerTests()
    {
        _tierEnforcementMock = new Mock<ITierEnforcementService>();
        _bggApiClientMock = new Mock<IBggApiClient>();
        _privateGameRepoMock = new Mock<IPrivateGameRepository>();
        _userLibraryRepoMock = new Mock<IUserLibraryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ImportBggGameCommandHandler>>();

        _sut = new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object,
            _bggApiClientMock.Object,
            _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region Happy Path

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsImportResult()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();

        var command = CreateCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.PrivateGameId);
        Assert.NotEqual(Guid.Empty, result.LibraryEntryId);
        Assert.Equal("Ticket to Ride", result.Title);
        Assert.Equal("https://example.com/image.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task Handle_WithValidCommand_PersistsPrivateGame()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();

        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _privateGameRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_PersistsLibraryEntry()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();

        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _userLibraryRepoMock.Verify(
            x => x.AddForPrivateGameAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesChanges()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();

        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RecordsUsageAfterSave()
    {
        // Arrange
        var callOrder = new List<string>();

        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();

        _privateGameRepoMock
            .Setup(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userLibraryRepoMock
            .Setup(x => x.AddForPrivateGameAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync(2);

        _tierEnforcementMock
            .Setup(x => x.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("record"))
            .Returns(Task.CompletedTask);

        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "save", "record" }, callOrder);
    }

    [Fact]
    public async Task Handle_WithValidCommand_LibraryEntryBelongsToUser()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();

        UserLibraryEntry? capturedEntry = null;
        _privateGameRepoMock
            .Setup(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userLibraryRepoMock
            .Setup(x => x.AddForPrivateGameAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<UserLibraryEntry, CancellationToken>((e, _) => capturedEntry = e)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _tierEnforcementMock
            .Setup(x => x.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedEntry);
        Assert.Equal(TestUserId, capturedEntry!.UserId);
    }

    #endregion

    #region Tier Quota Enforcement

    [Fact]
    public async Task Handle_WhenQuotaExceeded_ThrowsQuotaExceededException()
    {
        // Arrange
        _tierEnforcementMock
            .Setup(x => x.CanPerformAsync(It.IsAny<Guid>(), TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateCommand();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<QuotaExceededException>(
            () => _sut.Handle(command, CancellationToken.None));

        Assert.Equal("PrivateGameQuota", ex.QuotaType);
    }

    [Fact]
    public async Task Handle_WhenQuotaExceeded_DoesNotCallBggApi()
    {
        // Arrange
        _tierEnforcementMock
            .Setup(x => x.CanPerformAsync(It.IsAny<Guid>(), TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateCommand();

        // Act
        await Assert.ThrowsAsync<QuotaExceededException>(
            () => _sut.Handle(command, CancellationToken.None));

        // Assert
        _bggApiClientMock.Verify(
            x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Duplicate Detection

    [Fact]
    public async Task Handle_WhenBggIdAlreadyImported_ThrowsConflictException()
    {
        // Arrange
        SetupTierAllowed();
        _privateGameRepoMock
            .Setup(x => x.ExistsByOwnerAndBggIdAsync(TestUserId, TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateCommand();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDuplicate_DoesNotCallBggApi()
    {
        // Arrange
        SetupTierAllowed();
        _privateGameRepoMock
            .Setup(x => x.ExistsByOwnerAndBggIdAsync(TestUserId, TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateCommand();

        // Act
        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.Handle(command, CancellationToken.None));

        // Assert
        _bggApiClientMock.Verify(
            x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region BGG Not Found

    [Fact]
    public async Task Handle_WhenBggGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetails?)null);

        var command = CreateCommand();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenBggGameNotFound_DoesNotPersist()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetails?)null);

        var command = CreateCommand();

        // Act
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(command, CancellationToken.None));

        // Assert
        _privateGameRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullTierEnforcement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            null!, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullBggApiClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, null!, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullPrivateGameRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, null!,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullUserLibraryRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            null!, _unitOfWorkMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, null!));
    }

    #endregion

    #region Helpers

    private ImportBggGameCommand CreateCommand() =>
        new ImportBggGameCommand(UserId: TestUserId, BggId: TestBggId);

    private void SetupTierAllowed()
    {
        _tierEnforcementMock
            .Setup(x => x.CanPerformAsync(It.IsAny<Guid>(), TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void SetupNoDuplicate()
    {
        _privateGameRepoMock
            .Setup(x => x.ExistsByOwnerAndBggIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void SetupBggDetails()
    {
        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = TestBggId,
                Title = "Ticket to Ride",
                Description = "A cross-country train adventure game.",
                YearPublished = 2004,
                MinPlayers = 2,
                MaxPlayers = 5,
                PlayingTimeMinutes = 60,
                ComplexityRating = 1.9m,
                ImageUrl = "https://example.com/image.jpg",
                ThumbnailUrl = "https://example.com/thumb.jpg"
            });
    }

    private void SetupRepoDefaults()
    {
        _privateGameRepoMock
            .Setup(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userLibraryRepoMock
            .Setup(x => x.AddForPrivateGameAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _tierEnforcementMock
            .Setup(x => x.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    #endregion
}
