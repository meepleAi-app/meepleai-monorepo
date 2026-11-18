using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
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
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.PrivateGameId.Should().NotBe(Guid.Empty);
        result.LibraryEntryId.Should().NotBe(Guid.Empty);
        result.Title.Should().Be("Ticket to Ride");
        result.ImageUrl.Should().BeNull("#5066: the BGG image is not stored, so the result has none");
    }

    [Fact]
    public async Task Handle_WithValidCommand_DoesNotStoreTheBggImageUrls()
    {
        // #5066: hard ban on browser requests to BGG hosts (#2123, ADR-059 §5). Manual creation already stored
        // null; the import stored BGG's <image>/<thumbnail>, which /games/{id} then rendered in a plain <img>.
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();
        PrivateGame? stored = null;
        _privateGameRepoMock
            .Setup(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Callback<PrivateGame, CancellationToken>((game, _) => stored = game)
            .Returns(Task.CompletedTask);

        await _sut.Handle(CreateCommand(), CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.ImageUrl.Should().BeNull();
        stored.ThumbnailUrl.Should().BeNull();
        stored.BggId.Should().Be(TestBggId, "the rest of the BGG data is still imported");
        stored.Title.Should().Be("Ticket to Ride");
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

        // Assert — the save is the transaction's (#5020): the count and the insert are one step
        _unitOfWorkMock.Verify(
            x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CountsUnderTheOwnersLockBeforeStaging()
    {
        // #5020: the lock first, then the count, then the rows — inside one transaction. No Redis counter any more.
        var callOrder = new List<string>();
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();
        _privateGameRepoMock
            .Setup(x => x.LockOwnerQuotaAsync(TestUserId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("lock"))
            .Returns(Task.CompletedTask);
        _privateGameRepoMock
            .Setup(x => x.CountByOwnerIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("count"))
            .ReturnsAsync(0);
        _privateGameRepoMock
            .Setup(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("add"))
            .Returns(Task.CompletedTask);

        await _sut.Handle(CreateCommand(), CancellationToken.None);

        // The first count is the cheap one before the BGG call; the second, under the lock, is the guard.
        callOrder.Should().Equal("count", "lock", "count", "add");
        _tierEnforcementMock.Verify(
            x => x.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        var command = CreateCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.UserId.Should().Be(TestUserId);
    }

    #endregion

    #region Tier Quota Enforcement

    [Fact]
    public async Task Handle_WhenTheUserHasTheMaximum_ThrowsThe402OfThePrivateGameQuota()
    {
        // #5020: the same rule and the same answer as manual creation. It was a 400 from a daily Redis counter.
        SetupTierAllowed();
        _privateGameRepoMock
            .Setup(x => x.CountByOwnerIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier.MaxPrivateGames);

        var act = () => _sut.Handle(CreateCommand(), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<PrivateGameQuotaExceededException>()).Which;
        ex.StatusCode.Should().Be(402);
        ex.ErrorCode.Should().Be("PRIVATE_GAME_QUOTA_EXCEEDED");
    }

    [Fact]
    public async Task Handle_WhenTheUserHasTheMaximum_DoesNotCallBggApi()
    {
        SetupTierAllowed();
        _privateGameRepoMock
            .Setup(x => x.CountByOwnerIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier.MaxPrivateGames);

        var act = () => _sut.Handle(CreateCommand(), CancellationToken.None);
        await act.Should().ThrowAsync<PrivateGameQuotaExceededException>();

        _bggApiClientMock.Verify(
            x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheLimitIsReachedMeanwhile_TheCountUnderTheLockRefusesAndNothingIsStaged()
    {
        // The cheap count before the BGG call sees room; a concurrent creation fills it before the lock. The count
        // under the lock is the one that decides.
        SetupTierAllowed();
        SetupNoDuplicate();
        SetupBggDetails();
        SetupRepoDefaults();
        _privateGameRepoMock
            .SetupSequence(x => x.CountByOwnerIdAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier.MaxPrivateGames - 1)
            .ReturnsAsync(TierLimits.FreeTier.MaxPrivateGames);

        var act = () => _sut.Handle(CreateCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<PrivateGameQuotaExceededException>();
        _privateGameRepoMock.Verify(x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _userLibraryRepoMock.Verify(
            x => x.AddForPrivateGameAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
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
        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

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
            .ReturnsAsync((BggGameDetails)null!);

        var command = CreateCommand();

        // Act & Assert
        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenBggGameNotFound_DoesNotPersist()
    {
        // Arrange
        SetupTierAllowed();
        SetupNoDuplicate();
        _bggApiClientMock
            .Setup(x => x.GetGameDetailsAsync(TestBggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetails)null!);

        var command = CreateCommand();

        // Act
        var act =
            () => _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        // Assert
        _privateGameRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullTierEnforcement_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            null!, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullBggApiClient_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, null!, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPrivateGameRepo_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, null!,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUserLibraryRepo_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            null!, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () => new ImportBggGameCommandHandler(
            _tierEnforcementMock.Object, _bggApiClientMock.Object, _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object, _unitOfWorkMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helpers

    private ImportBggGameCommand CreateCommand() =>
        new ImportBggGameCommand(UserId: TestUserId, BggId: TestBggId);

    /// <summary>The free tier, an owner with no private game yet, and a transaction that runs its work (#5020).</summary>
    private void SetupTierAllowed()
    {
        _tierEnforcementMock
            .Setup(x => x.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier);
        _privateGameRepoMock
            .Setup(x => x.CountByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> work, CancellationToken ct) => work(ct));
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
                // What BGG's XML API answers: its own CDN.
                ImageUrl = "https://cf.geekdo-images.com/ttr/pic38668.jpg",
                ThumbnailUrl = "https://cf.geekdo-images.com/ttr/pic38668_t.jpg"
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

    }

    #endregion
}
