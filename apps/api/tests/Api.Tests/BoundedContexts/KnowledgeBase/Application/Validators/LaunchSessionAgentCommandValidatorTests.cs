using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for LaunchSessionAgentCommandValidator — semantic validations V1–V4.
/// Issue #2500: agent definition exists/active/game-match + JSON safe-parse.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LaunchSessionAgentCommandValidatorTests
{
    private static readonly Guid _gameSessionId = Guid.NewGuid();
    private static readonly Guid _agentDefinitionId = Guid.NewGuid();
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid _gameId = Guid.NewGuid();

    // ActivePlayer must be a valid Guid (GameState.Create validates Guid != Empty)
    private const string ValidGameStateJson =
        """{"CurrentTurn":1,"ActivePlayer":"11111111-1111-1111-1111-111111111111","PlayerScores":{},"GamePhase":"setup","LastAction":"start"}""";

    private readonly Mock<IAgentDefinitionRepository> _repoMock;
    private readonly LaunchSessionAgentCommandValidator _validator;

    public LaunchSessionAgentCommandValidatorTests()
    {
        _repoMock = new Mock<IAgentDefinitionRepository>();
        _validator = new LaunchSessionAgentCommandValidator(_repoMock.Object);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private LaunchSessionAgentCommand ValidCommand(
        Guid? agentDefinitionId = null,
        Guid? gameId = null,
        string? gameStateJson = null) =>
        new(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: agentDefinitionId ?? _agentDefinitionId,
            UserId: _userId,
            GameId: gameId ?? _gameId,
            InitialGameStateJson: gameStateJson ?? ValidGameStateJson);

    /// <summary>Creates a valid active AgentDefinition associated with the given game.</summary>
    private static AgentDefinition CreateActiveDefinition(Guid? gameId = null)
    {
        var def = AgentDefinition.Create(
            "Test Agent",
            "Description",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());

        def.Activate();

        if (gameId.HasValue)
            def.SetGameId(gameId.Value);

        return def;
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Existing base-field rules still pass (regression guard)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_EmptyGameSessionId_ReturnsError()
    {
        var command = new LaunchSessionAgentCommand(
            GameSessionId: Guid.Empty,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: ValidGameStateJson);

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.GameSessionId);
    }

    [Fact]
    public async Task Validate_EmptyAgentDefinitionId_ReturnsError_AndDoesNotCallRepo()
    {
        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: Guid.Empty,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: ValidGameStateJson);

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId);
        // Repo should NOT be called when the id is Guid.Empty (When gate)
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // V1 — AgentDefinition exists
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_V1_AgentDefinitionNotFound_ReturnsError()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        var command = ValidCommand();
        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId)
            .WithErrorMessage("AgentDefinition not found or has been deleted.");
    }

    /// <summary>
    /// I2 fix: consolidated V1/V2/V3 must produce exactly ONE error and call GetByIdAsync exactly ONCE
    /// when the definition is not found (not 3 errors + 3 queries like before).
    /// </summary>
    [Fact]
    public async Task Validate_V1_AgentDefinitionNotFound_ExactlyOneError_ExactlyOneDbCall()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        var command = ValidCommand();
        var result = await _validator.TestValidateAsync(command);

        // I2: exactly 1 error on AgentDefinitionId (not 3)
        var definitionErrors = result.Errors
            .Where(e => e.PropertyName == nameof(LaunchSessionAgentCommand.AgentDefinitionId))
            .ToList();
        Assert.Single(definitionErrors);

        // I2: GetByIdAsync called exactly ONCE (not 3 times)
        _repoMock.Verify(
            r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // V2 — AgentDefinition is active
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_V2_InactiveAgentDefinition_ReturnsError()
    {
        var inactiveDef = AgentDefinition.Create(
            "Inactive Agent",
            "Description",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());
        // NOT activated → IsActive = false
        inactiveDef.SetGameId(_gameId);

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveDef);

        var command = ValidCommand();
        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId)
            .WithErrorMessage("AgentDefinition is not active.");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // V3 — GameId match
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_V3_GameIdMismatch_ReturnsError()
    {
        var otherGameId = Guid.NewGuid();
        var def = CreateActiveDefinition(gameId: otherGameId); // different game

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = ValidCommand(); // command.GameId = _gameId
        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId)
            .WithErrorMessage("AgentDefinition does not belong to the specified game.");
    }

    [Fact]
    public async Task Validate_V3_NullGameIdOnDefinition_ReturnsError()
    {
        // definition.GameId is null → does not match any Guid → should fail
        var def = CreateActiveDefinition(gameId: null); // no game set

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = ValidCommand();
        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.AgentDefinitionId)
            .WithErrorMessage("AgentDefinition does not belong to the specified game.");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // V4 — InitialGameStateJson safe-parse
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_V4_MalformedJson_ReturnsError()
    {
        // Repo not needed for V4 (but set up to avoid null on V1/V2/V3 if the definition is loaded)
        // For this test we can return null from repo since we want to isolate the JSON rule.
        // However, the When gate on V1-V3 requires id != Guid.Empty.
        // Provide a valid definition so V1-V3 pass and only V4 fires.
        var def = CreateActiveDefinition(gameId: _gameId);
        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = ValidCommand(gameStateJson: "not-valid-json{{{");
        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.InitialGameStateJson)
            .WithErrorMessage("InitialGameStateJson is not a valid game state.");
    }

    [Fact]
    public async Task Validate_V4_EmptyStringJson_IsOptional_NoError()
    {
        // C1 fix: InitialGameStateJson is optional — empty string means "use server default".
        // V4 is gated with When(!IsNullOrWhiteSpace) so empty string must produce NO error on this field.
        var def = CreateActiveDefinition(gameId: _gameId);
        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: string.Empty);

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(x => x.InitialGameStateJson);
    }

    [Fact]
    public async Task Validate_V4_WhitespaceJson_IsOptional_NoError()
    {
        // Whitespace is also treated as "use server default" — no error on InitialGameStateJson.
        var def = CreateActiveDefinition(gameId: _gameId);
        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: "   ");

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(x => x.InitialGameStateJson);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Happy path — all validations pass
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_HappyPath_ActiveDefinitionMatchingGame_ValidJson_NoErrors()
    {
        var def = CreateActiveDefinition(gameId: _gameId);

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = ValidCommand();
        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_HappyPath_EmptyGameStateJson_NoErrors()
    {
        // C1 fix: empty InitialGameStateJson is valid (handler uses GameState.Initial).
        var def = CreateActiveDefinition(gameId: _gameId);

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        var command = ValidCommand(gameStateJson: string.Empty);
        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_HappyPath_MinimalValidJson_NoErrors()
    {
        var def = CreateActiveDefinition(gameId: _gameId);

        _repoMock
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(def);

        // Minimal valid GameState JSON (all required fields; ActivePlayer must be a valid non-empty Guid)
        const string minimalJson =
            """{"CurrentTurn":1,"ActivePlayer":"22222222-2222-2222-2222-222222222222","PlayerScores":{},"GamePhase":"setup","LastAction":"none"}""";

        var command = ValidCommand(gameStateJson: minimalJson);
        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
