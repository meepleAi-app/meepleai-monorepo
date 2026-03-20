using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for StartImprovvisataSessionCommandHandler.
/// Game Night Improvvisata - E2-1: Start session from PrivateGame.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class StartImprovvisataSessionCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly StartImprovvisataSessionCommandHandler _sut;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid TestPrivateGameId = Guid.NewGuid();

    public StartImprovvisataSessionCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _tierEnforcementMock = new Mock<ITierEnforcementService>();

        // Default: user is within quota
        _tierEnforcementMock
            .Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tierEnforcementMock
            .Setup(t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new StartImprovvisataSessionCommandHandler(
            _dbContext,
            _sessionRepoMock.Object,
            _tierEnforcementMock.Object,
            TimeProvider.System,
            NullLogger<StartImprovvisataSessionCommandHandler>.Instance);
    }

    private async Task SeedPrivateGame(Guid gameId, Guid ownerId, string title = "Test Game")
    {
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = ownerId,
            Title = title,
            MinPlayers = 2,
            MaxPlayers = 4,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ReturnsSessionIdAndInviteCode()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId, "Wingspan");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.SessionId.Should().NotBe(Guid.Empty);
        Assert.NotNull(result.InviteCode);
        result.InviteCode.Length.Should().Be(6);
        Assert.StartsWith("/join/", result.ShareLink);
        Assert.Equal(32, result.ShareLink.Length - "/join/".Length); // UUID without hyphens = 32 chars
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsLiveGameSessionToDatabase()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId, "Wingspan");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var savedSession = await _dbContext.LiveGameSessions.FindAsync(result.SessionId);
        Assert.NotNull(savedSession);
        savedSession.GameName.Should().Be("Wingspan");
        savedSession.CreatedByUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsSessionInviteToDatabase()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId);
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var savedInvite = _dbContext.SessionInvites.FirstOrDefault(i => i.SessionId == result.SessionId);
        Assert.NotNull(savedInvite);
        savedInvite.Pin.Should().Be(result.InviteCode);
        savedInvite.CreatedByUserId.Should().Be(TestUserId);
        Assert.False(savedInvite.IsRevoked);
        savedInvite.CurrentUses.Should().Be(0);
    }

    [Fact]
    public async Task Handle_HappyPath_InviteHas24HourExpiry()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId);
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var savedInvite = _dbContext.SessionInvites.FirstOrDefault(i => i.SessionId == result.SessionId);
        Assert.NotNull(savedInvite);
        var expectedExpiry = DateTime.UtcNow.AddHours(23); // at least 23h from now
        Assert.True(savedInvite.ExpiresAt > expectedExpiry,
            $"Expected ExpiresAt > {expectedExpiry:u} but was {savedInvite.ExpiresAt:u}");
    }

    [Fact]
    public async Task Handle_HappyPath_InviteHasMaxTenUses()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId);
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var savedInvite = _dbContext.SessionInvites.FirstOrDefault(i => i.SessionId == result.SessionId);
        Assert.NotNull(savedInvite);
        savedInvite.MaxUses.Should().Be(10);
    }

    [Fact]
    public async Task Handle_HappyPath_RegistersSessionInLiveSessionRepository()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId);
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _sessionRepoMock.Verify(
            r => r.AddAsync(It.Is<LiveGameSession>(s => s.CreatedByUserId == TestUserId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_UserIsAddedAsHost()
    {
        // Arrange
        await SeedPrivateGame(TestPrivateGameId, TestUserId);
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        LiveGameSession? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.NotNull(capturedSession.Host);
        capturedSession.Host!.UserId.Should().Be(TestUserId);
    }

    // ─── Error cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPrivateGameNotFound_ThrowsNotFoundException()
    {
        // Arrange — no game seeded
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPrivateGameIsDeleted_ThrowsNotFoundException()
    {
        // Arrange — game exists but soft-deleted
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = TestPrivateGameId,
            OwnerId = TestUserId,
            Title = "Deleted Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        });
        await _dbContext.SaveChangesAsync();

        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnGame_ThrowsForbiddenException()
    {
        // Arrange — game owned by a different user
        await SeedPrivateGame(TestPrivateGameId, OtherUserId, "Someone Else's Game");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    // ─── Tier enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTierQuotaExceeded_ThrowsConflictException()
    {
        // Arrange — tier returns false for CanPerform
        _tierEnforcementMock
            .Setup(t => t.CanPerformAsync(TestUserId, TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _tierEnforcementMock
            .Setup(t => t.GetUsageAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageSnapshot(
                PrivateGames: 3, PrivateGamesMax: 3,
                PdfThisMonth: 0, PdfThisMonthMax: 3,
                AgentQueriesToday: 0, AgentQueriesTodayMax: 20,
                SessionQueries: 0, SessionQueriesMax: 30,
                Agents: 0, AgentsMax: 1,
                PhotosThisSession: 0, PhotosThisSessionMax: 5,
                SessionSaveEnabled: false,
                CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 1));

        await SeedPrivateGame(TestPrivateGameId, TestUserId, "Wingspan");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));

        Assert.Contains("limite di giochi privati", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3/3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_WhenWithinTierQuota_SucceedsAndRecordsUsage()
    {
        // Arrange — default mock allows the action
        await SeedPrivateGame(TestPrivateGameId, TestUserId, "Wingspan");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — session created and usage recorded
        result.SessionId.Should().NotBe(Guid.Empty);
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(TestUserId, TierAction.CreatePrivateGame, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// Validation tests for StartImprovvisataSessionValidator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class StartImprovvisataSessionValidatorTests
{
    private readonly StartImprovvisataSessionValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var command = new StartImprovvisataSessionCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPrivateGameId_Fails()
    {
        var command = new StartImprovvisataSessionCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId);
    }

    [Fact]
    public void Validate_WithEmptyUserId_Fails()
    {
        var command = new StartImprovvisataSessionCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_WithBothEmpty_FailsBothFields()
    {
        var command = new StartImprovvisataSessionCommand(Guid.Empty, Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId);
    }
}
