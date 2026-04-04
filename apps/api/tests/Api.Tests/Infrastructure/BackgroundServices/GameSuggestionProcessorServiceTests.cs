using System.Threading.Channels;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.BackgroundServices;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="GameSuggestionProcessorService"/>.
/// Admin Invitation Flow: verifies that game suggestions from the channel
/// are correctly processed as PreAdded (library + suggestion) or Suggested (suggestion only).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GameSuggestionProcessorServiceTests
{
    private readonly GameSuggestionChannel _channel;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IGameSuggestionRepository> _gameSuggestionRepoMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock;
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly TestTimeProvider _timeProvider;
    private readonly GameSuggestionProcessorService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid InvitedByUserId = Guid.NewGuid();
    private static readonly Guid GameId1 = Guid.NewGuid();
    private static readonly Guid GameId2 = Guid.NewGuid();

    public GameSuggestionProcessorServiceTests()
    {
        _channel = new GameSuggestionChannel();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _gameSuggestionRepoMock = new Mock<IGameSuggestionRepository>();
        _sharedGameRepoMock = new Mock<ISharedGameRepository>();
        _libraryRepoMock = new Mock<IUserLibraryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProvider = new TestTimeProvider();

        // Wire scope chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        // Register handlers via service provider
        var preAddedHandler = new GamePreAddedHandler(
            _sharedGameRepoMock.Object,
            _libraryRepoMock.Object,
            _gameSuggestionRepoMock.Object,
            _timeProvider,
            NullLogger<GamePreAddedHandler>.Instance);

        var suggestedHandler = new GameSuggestedHandler(
            _gameSuggestionRepoMock.Object,
            _timeProvider,
            NullLogger<GameSuggestedHandler>.Instance);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(GamePreAddedHandler)))
            .Returns(preAddedHandler);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(GameSuggestedHandler)))
            .Returns(suggestedHandler);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IUnitOfWork)))
            .Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Default: games exist in catalog, not in library, no existing suggestions
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), _timeProvider));
        _libraryRepoMock
            .Setup(r => r.IsGameInLibraryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _gameSuggestionRepoMock
            .Setup(r => r.ExistsForUserAndGameAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _libraryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gameSuggestionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new GameSuggestionProcessorService(
            _channel,
            _scopeFactoryMock.Object,
            NullLogger<GameSuggestionProcessorService>.Instance);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InvitationGameSuggestion CreateSuggestion(Guid gameId, GameSuggestionType type)
    {
        return InvitationGameSuggestion.Create(Guid.NewGuid(), gameId, type);
    }

    // ── PreAdded suggestion adds game to library ────────────────────────────

    [Fact]
    public async Task ProcessEvent_PreAddedSuggestion_AddsGameToLibraryAndCreatesSuggestion()
    {
        // Arrange
        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.PreAdded) });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: game added to library
        _libraryRepoMock.Verify(
            r => r.AddAsync(It.Is<UserLibraryEntry>(e => e.UserId == UserId && e.GameId == GameId1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: suggestion record created and accepted
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.Is<GameSuggestion>(s =>
                s.UserId == UserId && s.GameId == GameId1 && s.IsAccepted),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: changes saved
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Suggested suggestion creates GameSuggestion only ────────────────────

    [Fact]
    public async Task ProcessEvent_SuggestedSuggestion_CreatesGameSuggestionWithoutLibraryEntry()
    {
        // Arrange
        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.Suggested) });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: no library entry added
        _libraryRepoMock.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert: suggestion created (not accepted)
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.Is<GameSuggestion>(s =>
                s.UserId == UserId && s.GameId == GameId1 && !s.IsAccepted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── PreAdded is idempotent (game already in library) ────────────────────

    [Fact]
    public async Task ProcessEvent_PreAdded_GameAlreadyInLibrary_SkipsWithoutError()
    {
        // Arrange
        _libraryRepoMock
            .Setup(r => r.IsGameInLibraryAsync(UserId, GameId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.PreAdded) });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: no library entry added
        _libraryRepoMock.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Suggested is idempotent (suggestion already exists) ─────────────────

    [Fact]
    public async Task ProcessEvent_Suggested_SuggestionAlreadyExists_SkipsWithoutError()
    {
        // Arrange
        _gameSuggestionRepoMock
            .Setup(r => r.ExistsForUserAndGameAsync(UserId, GameId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.Suggested) });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: no suggestion added
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── PreAdded skips when game not in catalog ─────────────────────────────

    [Fact]
    public async Task ProcessEvent_PreAdded_GameNotInCatalog_SkipsWithoutError()
    {
        // Arrange
        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(GameId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.PreAdded) });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: neither library entry nor suggestion created
        _libraryRepoMock.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Mixed suggestions in single event ───────────────────────────────────

    [Fact]
    public async Task ProcessEvent_MixedSuggestions_ProcessesBothTypes()
    {
        // Arrange
        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[]
            {
                CreateSuggestion(GameId1, GameSuggestionType.PreAdded),
                CreateSuggestion(GameId2, GameSuggestionType.Suggested)
            });

        // Act
        await _sut.ProcessEventAsync(evt, CancellationToken.None);

        // Assert: one library entry (PreAdded) and two suggestion records
        _libraryRepoMock.Verify(
            r => r.AddAsync(It.Is<UserLibraryEntry>(e => e.GameId == GameId1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // PreAdded creates accepted suggestion, Suggested creates unaccepted suggestion
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Channel integration: events are read and processed ──────────────────

    [Fact]
    public async Task ExecuteAsync_ReadsEventsFromChannel_AndProcessesThem()
    {
        // Arrange
        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.Suggested) });

        // Write event to channel then complete
        await _channel.Writer.WriteAsync(evt);
        _channel.Writer.Complete();

        // Act: run the service until channel is complete
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sut.StartAsync(cts.Token);

        // Give it a moment to process
        await Task.Delay(500);
        await _sut.StopAsync(CancellationToken.None);

        // Assert: suggestion was created
        _gameSuggestionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Service does not crash on processing failure ────────────────────────

    [Fact]
    public async Task ProcessEvent_OnFailure_DoesNotCrashService()
    {
        // Arrange: make the unit of work throw
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

        var evt = new GameSuggestionEvent(
            UserId, InvitedByUserId,
            new[] { CreateSuggestion(GameId1, GameSuggestionType.Suggested) });

        // Write event then complete channel
        await _channel.Writer.WriteAsync(evt);
        _channel.Writer.Complete();

        // Act: run the service (should not throw)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _sut.StartAsync(cts.Token);

        // Give it time to process (including retries)
        await Task.Delay(6000);
        await _sut.StopAsync(CancellationToken.None);

        // Assert: service attempted processing (retries count)
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2)); // At least retry once
    }
}
