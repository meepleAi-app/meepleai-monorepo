using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Unit tests for all LiveSession command validators.
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LiveSessionValidatorTests
{
    // ──────────────────────────────────────────────────────────────────
    // CreateLiveSessionCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region CreateLiveSessionCommandValidator

    private readonly CreateLiveSessionCommandValidator _createSessionValidator = new();

    private static CreateLiveSessionCommand ValidCreateCommand() => new(
        UserId: Guid.NewGuid(),
        GameName: "Catan",
        GameId: Guid.NewGuid(),
        Visibility: PlayRecordVisibility.Private,
        GroupId: null,
        ScoringDimensions: null,
        DimensionUnits: null,
        AgentMode: AgentSessionMode.None
    );

    [Fact]
    public async Task CreateSession_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand();

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_EmptyUserId_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with { UserId = Guid.Empty };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSession_EmptyGameName_ShouldFail(string? gameName)
    {
        // Arrange
        var command = ValidCreateCommand() with { GameName = gameName! };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameName)
            .WithErrorMessage("Game name is required");
    }

    [Fact]
    public async Task CreateSession_GameNameExceeds255Characters_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with { GameName = new string('a', 256) };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameName)
            .WithErrorMessage("Game name cannot exceed 255 characters");
    }

    [Fact]
    public async Task CreateSession_GameNameExactly255Characters_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand() with { GameName = new string('a', 255) };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_InvalidVisibilityEnum_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with { Visibility = (PlayRecordVisibility)999 };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Visibility)
            .WithErrorMessage("Invalid visibility value");
    }

    [Fact]
    public async Task CreateSession_GroupVisibilityWithoutGroupId_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with
        {
            Visibility = PlayRecordVisibility.Group,
            GroupId = null
        };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GroupId)
            .WithErrorMessage("GroupId is required when visibility is Group");
    }

    [Fact]
    public async Task CreateSession_GroupVisibilityWithValidGroupId_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand() with
        {
            Visibility = PlayRecordVisibility.Group,
            GroupId = Guid.NewGuid()
        };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_PrivateVisibilityWithoutGroupId_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand() with
        {
            Visibility = PlayRecordVisibility.Private,
            GroupId = null
        };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_InvalidAgentModeEnum_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with { AgentMode = (AgentSessionMode)999 };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AgentMode)
            .WithErrorMessage("Invalid agent mode value");
    }

    [Theory]
    [InlineData(AgentSessionMode.None)]
    [InlineData(AgentSessionMode.Assistant)]
    [InlineData(AgentSessionMode.GameMaster)]
    public async Task CreateSession_ValidAgentMode_ShouldPass(AgentSessionMode mode)
    {
        // Arrange
        var command = ValidCreateCommand() with { AgentMode = mode };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_EmptyScoringDimensionsList_ShouldFail()
    {
        // Arrange
        var command = ValidCreateCommand() with { ScoringDimensions = new List<string>() };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ScoringDimensions)
            .WithErrorMessage("Scoring dimensions cannot be empty if provided");
    }

    [Fact]
    public async Task CreateSession_TooManyScoringDimensions_ShouldFail()
    {
        // Arrange
        var dimensions = Enumerable.Range(1, 11).Select(i => $"dim{i}").ToList();
        var command = ValidCreateCommand() with { ScoringDimensions = dimensions };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ScoringDimensions)
            .WithErrorMessage("Maximum 10 scoring dimensions allowed");
    }

    [Fact]
    public async Task CreateSession_NullScoringDimensions_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand() with { ScoringDimensions = null };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_ValidScoringDimensions_ShouldPass()
    {
        // Arrange
        var command = ValidCreateCommand() with
        {
            ScoringDimensions = new List<string> { "Points", "Gold", "Victory" }
        };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateSession_ExactlyTenScoringDimensions_ShouldPass()
    {
        // Arrange
        var dimensions = Enumerable.Range(1, 10).Select(i => $"dim{i}").ToList();
        var command = ValidCreateCommand() with { ScoringDimensions = dimensions };

        // Act
        var result = await _createSessionValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // AddPlayerToLiveSessionCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region AddPlayerToLiveSessionCommandValidator

    private readonly AddPlayerToLiveSessionCommandValidator _addPlayerValidator = new();

    private static AddPlayerToLiveSessionCommand ValidAddPlayerCommand() => new(
        SessionId: Guid.NewGuid(),
        DisplayName: "Player One",
        Color: PlayerColor.Red,
        UserId: Guid.NewGuid(),
        Role: PlayerRole.Player,
        AvatarUrl: "https://example.com/avatar.png"
    );

    [Fact]
    public async Task AddPlayer_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = ValidAddPlayerCommand();

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AddPlayer_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { SessionId = Guid.Empty };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddPlayer_EmptyDisplayName_ShouldFail(string? displayName)
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { DisplayName = displayName! };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name is required");
    }

    [Fact]
    public async Task AddPlayer_DisplayNameExceeds100Characters_ShouldFail()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { DisplayName = new string('a', 101) };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name cannot exceed 100 characters");
    }

    [Fact]
    public async Task AddPlayer_DisplayNameExactly100Characters_ShouldPass()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { DisplayName = new string('a', 100) };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AddPlayer_InvalidColorEnum_ShouldFail()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { Color = (PlayerColor)999 };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Color)
            .WithErrorMessage("Invalid player color");
    }

    [Theory]
    [InlineData(PlayerColor.Red)]
    [InlineData(PlayerColor.Blue)]
    [InlineData(PlayerColor.Green)]
    [InlineData(PlayerColor.Yellow)]
    [InlineData(PlayerColor.Purple)]
    [InlineData(PlayerColor.Orange)]
    [InlineData(PlayerColor.Pink)]
    [InlineData(PlayerColor.Teal)]
    public async Task AddPlayer_ValidColor_ShouldPass(PlayerColor color)
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { Color = color };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AddPlayer_InvalidRoleEnum_ShouldFail()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { Role = (PlayerRole)999 };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Invalid player role");
    }

    [Fact]
    public async Task AddPlayer_NullRole_ShouldPass()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { Role = null };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AddPlayer_AvatarUrlExceeds500Characters_ShouldFail()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { AvatarUrl = new string('a', 501) };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AvatarUrl)
            .WithErrorMessage("Avatar URL cannot exceed 500 characters");
    }

    [Fact]
    public async Task AddPlayer_AvatarUrlExactly500Characters_ShouldPass()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { AvatarUrl = new string('a', 500) };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AddPlayer_NullAvatarUrl_ShouldPass()
    {
        // Arrange
        var command = ValidAddPlayerCommand() with { AvatarUrl = null };

        // Act
        var result = await _addPlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // RemovePlayerFromLiveSessionCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region RemovePlayerFromLiveSessionCommandValidator

    private readonly RemovePlayerFromLiveSessionCommandValidator _removePlayerValidator = new();

    [Fact]
    public async Task RemovePlayer_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new RemovePlayerFromLiveSessionCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _removePlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RemovePlayer_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new RemovePlayerFromLiveSessionCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = await _removePlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task RemovePlayer_EmptyPlayerId_ShouldFail()
    {
        // Arrange
        var command = new RemovePlayerFromLiveSessionCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = await _removePlayerValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerId)
            .WithErrorMessage("Player ID is required");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // UpdatePlayerOrderCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region UpdatePlayerOrderCommandValidator

    private readonly UpdatePlayerOrderCommandValidator _updateOrderValidator = new();

    [Fact]
    public async Task UpdatePlayerOrder_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new UpdatePlayerOrderCommand(
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        );

        // Act
        var result = await _updateOrderValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task UpdatePlayerOrder_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new UpdatePlayerOrderCommand(
            Guid.Empty,
            new List<Guid> { Guid.NewGuid() }
        );

        // Act
        var result = await _updateOrderValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task UpdatePlayerOrder_NullPlayerIds_ShouldFail()
    {
        // Arrange
        var command = new UpdatePlayerOrderCommand(Guid.NewGuid(), null!);

        // Act
        var result = await _updateOrderValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerIds)
            .WithErrorMessage("Player IDs list is required");
    }

    [Fact]
    public async Task UpdatePlayerOrder_EmptyPlayerIdsList_ShouldFail()
    {
        // Arrange
        var command = new UpdatePlayerOrderCommand(Guid.NewGuid(), new List<Guid>());

        // Act
        var result = await _updateOrderValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerIds)
            .WithErrorMessage("At least one player ID is required");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // CreateLiveSessionTeamCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region CreateLiveSessionTeamCommandValidator

    private readonly CreateLiveSessionTeamCommandValidator _createTeamValidator = new();

    [Fact]
    public async Task CreateTeam_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), "Alpha", "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CreateTeam_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.Empty, "Alpha", "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTeam_EmptyName_ShouldFail(string? name)
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), name!, "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Team name is required");
    }

    [Fact]
    public async Task CreateTeam_NameExceeds50Characters_ShouldFail()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), new string('a', 51), "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Team name cannot exceed 50 characters");
    }

    [Fact]
    public async Task CreateTeam_NameExactly50Characters_ShouldPass()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), new string('a', 50), "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTeam_EmptyColor_ShouldFail(string? color)
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), "Alpha", color!);

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Color)
            .WithErrorMessage("Team color is required");
    }

    [Fact]
    public async Task CreateTeam_ColorExceeds7Characters_ShouldFail()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), "Alpha", "#FF00001");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Color)
            .WithErrorMessage("Team color cannot exceed 7 characters");
    }

    [Fact]
    public async Task CreateTeam_ColorExactly7Characters_ShouldPass()
    {
        // Arrange
        var command = new CreateLiveSessionTeamCommand(Guid.NewGuid(), "Alpha", "#FF0000");

        // Act
        var result = await _createTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // AssignPlayerToTeamCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region AssignPlayerToTeamCommandValidator

    private readonly AssignPlayerToTeamCommandValidator _assignTeamValidator = new();

    [Fact]
    public async Task AssignTeam_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new AssignPlayerToTeamCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _assignTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AssignTeam_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new AssignPlayerToTeamCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _assignTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task AssignTeam_EmptyPlayerId_ShouldFail()
    {
        // Arrange
        var command = new AssignPlayerToTeamCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        // Act
        var result = await _assignTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerId)
            .WithErrorMessage("Player ID is required");
    }

    [Fact]
    public async Task AssignTeam_EmptyTeamId_ShouldFail()
    {
        // Arrange
        var command = new AssignPlayerToTeamCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        // Act
        var result = await _assignTeamValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TeamId)
            .WithErrorMessage("Team ID is required");
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // RecordLiveSessionScoreCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region RecordLiveSessionScoreCommandValidator

    private readonly RecordLiveSessionScoreCommandValidator _recordScoreValidator = new();

    private static RecordLiveSessionScoreCommand ValidRecordScoreCommand() => new(
        SessionId: Guid.NewGuid(),
        PlayerId: Guid.NewGuid(),
        Round: 1,
        Dimension: "Points",
        Value: 42,
        Unit: "VP"
    );

    [Fact]
    public async Task RecordScore_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = ValidRecordScoreCommand();

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RecordScore_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { SessionId = Guid.Empty };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task RecordScore_EmptyPlayerId_ShouldFail()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { PlayerId = Guid.Empty };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerId)
            .WithErrorMessage("Player ID is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task RecordScore_RoundNotGreaterThanZero_ShouldFail(int round)
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Round = round };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Round)
            .WithErrorMessage("Round must be at least 1");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public async Task RecordScore_ValidRound_ShouldPass(int round)
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Round = round };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordScore_EmptyDimension_ShouldFail(string? dimension)
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Dimension = dimension! };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Dimension)
            .WithErrorMessage("Score dimension is required");
    }

    [Fact]
    public async Task RecordScore_DimensionExceeds50Characters_ShouldFail()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Dimension = new string('a', 51) };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Dimension)
            .WithErrorMessage("Score dimension cannot exceed 50 characters");
    }

    [Fact]
    public async Task RecordScore_DimensionExactly50Characters_ShouldPass()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Dimension = new string('a', 50) };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RecordScore_UnitExceeds20Characters_ShouldFail()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Unit = new string('a', 21) };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Unit)
            .WithErrorMessage("Score unit cannot exceed 20 characters");
    }

    [Fact]
    public async Task RecordScore_UnitExactly20Characters_ShouldPass()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Unit = new string('a', 20) };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RecordScore_NullUnit_ShouldPass()
    {
        // Arrange
        var command = ValidRecordScoreCommand() with { Unit = null };

        // Act
        var result = await _recordScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // EditLiveSessionScoreCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region EditLiveSessionScoreCommandValidator

    private readonly EditLiveSessionScoreCommandValidator _editScoreValidator = new();

    private static EditLiveSessionScoreCommand ValidEditScoreCommand() => new(
        SessionId: Guid.NewGuid(),
        PlayerId: Guid.NewGuid(),
        Round: 1,
        Dimension: "Points",
        Value: 42,
        Unit: "VP"
    );

    [Fact]
    public async Task EditScore_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = ValidEditScoreCommand();

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EditScore_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = ValidEditScoreCommand() with { SessionId = Guid.Empty };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task EditScore_EmptyPlayerId_ShouldFail()
    {
        // Arrange
        var command = ValidEditScoreCommand() with { PlayerId = Guid.Empty };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayerId)
            .WithErrorMessage("Player ID is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task EditScore_RoundNotGreaterThanZero_ShouldFail(int round)
    {
        // Arrange
        var command = ValidEditScoreCommand() with { Round = round };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Round)
            .WithErrorMessage("Round must be at least 1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EditScore_EmptyDimension_ShouldFail(string? dimension)
    {
        // Arrange
        var command = ValidEditScoreCommand() with { Dimension = dimension! };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Dimension)
            .WithErrorMessage("Score dimension is required");
    }

    [Fact]
    public async Task EditScore_DimensionExceeds50Characters_ShouldFail()
    {
        // Arrange
        var command = ValidEditScoreCommand() with { Dimension = new string('a', 51) };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Dimension)
            .WithErrorMessage("Score dimension cannot exceed 50 characters");
    }

    [Fact]
    public async Task EditScore_UnitExceeds20Characters_ShouldFail()
    {
        // Arrange
        var command = ValidEditScoreCommand() with { Unit = new string('a', 21) };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Unit)
            .WithErrorMessage("Score unit cannot exceed 20 characters");
    }

    [Fact]
    public async Task EditScore_NullUnit_ShouldPass()
    {
        // Arrange
        var command = ValidEditScoreCommand() with { Unit = null };

        // Act
        var result = await _editScoreValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // UpdateLiveSessionNotesCommandValidator
    // ──────────────────────────────────────────────────────────────────

    #region UpdateLiveSessionNotesCommandValidator

    private readonly UpdateLiveSessionNotesCommandValidator _updateNotesValidator = new();

    [Fact]
    public async Task UpdateNotes_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new UpdateLiveSessionNotesCommand(Guid.NewGuid(), "Some session notes.");

        // Act
        var result = await _updateNotesValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task UpdateNotes_NullNotes_ShouldPass()
    {
        // Arrange
        var command = new UpdateLiveSessionNotesCommand(Guid.NewGuid(), null);

        // Act
        var result = await _updateNotesValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task UpdateNotes_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new UpdateLiveSessionNotesCommand(Guid.Empty, "Notes");

        // Act
        var result = await _updateNotesValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task UpdateNotes_NotesExceed2000Characters_ShouldFail()
    {
        // Arrange
        var command = new UpdateLiveSessionNotesCommand(Guid.NewGuid(), new string('a', 2001));

        // Act
        var result = await _updateNotesValidator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 2000 characters");
    }

    [Fact]
    public async Task UpdateNotes_NotesExactly2000Characters_ShouldPass()
    {
        // Arrange
        var command = new UpdateLiveSessionNotesCommand(Guid.NewGuid(), new string('a', 2000));

        // Act
        var result = await _updateNotesValidator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    // SessionId-only validators (Start, Pause, Resume, Complete, Save, AdvanceTurn)
    // ──────────────────────────────────────────────────────────────────

    #region SessionId-Only Validators

    private readonly StartLiveSessionCommandValidator _startValidator = new();
    private readonly PauseLiveSessionCommandValidator _pauseValidator = new();
    private readonly ResumeLiveSessionCommandValidator _resumeValidator = new();
    private readonly CompleteLiveSessionCommandValidator _completeValidator = new();
    private readonly SaveLiveSessionCommandValidator _saveValidator = new();
    private readonly AdvanceLiveSessionTurnCommandValidator _advanceTurnValidator = new();

    [Fact]
    public async Task Start_ValidSessionId_ShouldPass()
    {
        var result = await _startValidator.TestValidateAsync(new StartLiveSessionCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Start_EmptySessionId_ShouldFail()
    {
        var result = await _startValidator.TestValidateAsync(new StartLiveSessionCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task Pause_ValidSessionId_ShouldPass()
    {
        var result = await _pauseValidator.TestValidateAsync(new PauseLiveSessionCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Pause_EmptySessionId_ShouldFail()
    {
        var result = await _pauseValidator.TestValidateAsync(new PauseLiveSessionCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task Resume_ValidSessionId_ShouldPass()
    {
        var result = await _resumeValidator.TestValidateAsync(new ResumeLiveSessionCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Resume_EmptySessionId_ShouldFail()
    {
        var result = await _resumeValidator.TestValidateAsync(new ResumeLiveSessionCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task Complete_ValidSessionId_ShouldPass()
    {
        var result = await _completeValidator.TestValidateAsync(new CompleteLiveSessionCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Complete_EmptySessionId_ShouldFail()
    {
        var result = await _completeValidator.TestValidateAsync(new CompleteLiveSessionCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task Save_ValidSessionId_ShouldPass()
    {
        var result = await _saveValidator.TestValidateAsync(new SaveLiveSessionCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Save_EmptySessionId_ShouldFail()
    {
        var result = await _saveValidator.TestValidateAsync(new SaveLiveSessionCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    [Fact]
    public async Task AdvanceTurn_ValidSessionId_ShouldPass()
    {
        var result = await _advanceTurnValidator.TestValidateAsync(new AdvanceLiveSessionTurnCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task AdvanceTurn_EmptySessionId_ShouldFail()
    {
        var result = await _advanceTurnValidator.TestValidateAsync(new AdvanceLiveSessionTurnCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("Session ID is required");
    }

    #endregion
}
