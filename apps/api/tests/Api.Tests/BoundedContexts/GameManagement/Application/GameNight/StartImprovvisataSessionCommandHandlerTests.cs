using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
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
    private readonly StartImprovvisataSessionCommandHandler _sut;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid TestPrivateGameId = Guid.NewGuid();

    public StartImprovvisataSessionCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();

        // Simulate the EF-backed repository: stage a minimal entity in the DbContext so
        // the handler's subsequent SaveChangesAsync persists it for the FindAsync-based
        // assertions. Mirrors the LiveGameSessionMapper.ToEntity path the real repository
        // would take (Issue #2097 / ADR-060).
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns<LiveGameSession, CancellationToken>((session, _) =>
            {
                _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
                {
                    Id = session.Id,
                    SessionCode = session.SessionCode,
                    GameId = session.GameId,
                    GameName = session.GameName,
                    ToolkitId = session.ToolkitId,
                    CreatedByUserId = session.CreatedByUserId,
                    Visibility = (int)session.Visibility,
                    GroupId = session.GroupId,
                    Status = (int)session.Status,
                    CurrentTurnIndex = session.CurrentTurnIndex,
                    CreatedAt = session.CreatedAt,
                    UpdatedAt = session.UpdatedAt,
                    AgentMode = (int)session.AgentMode,
                    ScoringConfigJson = "{}",
                    Xmin = 0u
                });
                return Task.CompletedTask;
            });

        _sut = new StartImprovvisataSessionCommandHandler(
            _dbContext,
            _sessionRepoMock.Object,
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
        result.InviteCode.Should().NotBeNull();
        result.InviteCode.Length.Should().Be(6);
        result.ShareLink.Should().StartWith("/join/");
        (result.ShareLink.Length - "/join/".Length).Should().Be(32); // UUID without hyphens = 32 chars
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
        savedSession.Should().NotBeNull();
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
        savedInvite.Should().NotBeNull();
        savedInvite.Pin.Should().Be(result.InviteCode);
        savedInvite.CreatedByUserId.Should().Be(TestUserId);
        (savedInvite.IsRevoked).Should().BeFalse();
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
        savedInvite.Should().NotBeNull();
        var expectedExpiry = DateTime.UtcNow.AddHours(23); // at least 23h from now
        (savedInvite.ExpiresAt > expectedExpiry).Should().BeTrue($"Expected ExpiresAt > {expectedExpiry:u} but was {savedInvite.ExpiresAt:u}");
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
        savedInvite.Should().NotBeNull();
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
        capturedSession.Should().NotBeNull();
        capturedSession.Host.Should().NotBeNull();
        capturedSession.Host!.UserId.Should().Be(TestUserId);
    }

    // ─── Error cases ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPrivateGameNotFound_ThrowsNotFoundException()
    {
        // Arrange — no game seeded
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
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
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnGame_ThrowsForbiddenException()
    {
        // Arrange — game owned by a different user
        await SeedPrivateGame(TestPrivateGameId, OtherUserId, "Someone Else's Game");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ─── Private-game quota (#5020) ──────────────────────────────────────────

    [Fact]
    public async Task Handle_UserWithTheMaximumOfPrivateGames_StartsASessionWithOneOfThem()
    {
        // #5020 (decision of 2026-11-18): starting a session creates no private game, so the private-game quota does
        // not apply. It used to: a free user with 3 private games was refused a session with any of them, and the
        // handler depended on ITierEnforcementService only for that.
        await SeedPrivateGame(TestPrivateGameId, TestUserId, "Wingspan");
        await SeedPrivateGame(Guid.NewGuid(), TestUserId, "Azul");
        await SeedPrivateGame(Guid.NewGuid(), TestUserId, "Cascadia");
        var command = new StartImprovvisataSessionCommand(TestUserId, TestPrivateGameId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.SessionId.Should().NotBe(Guid.Empty);
        (await _dbContext.LiveGameSessions.FindAsync(new object[] { result.SessionId }, CancellationToken.None))
            .Should().NotBeNull();
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
