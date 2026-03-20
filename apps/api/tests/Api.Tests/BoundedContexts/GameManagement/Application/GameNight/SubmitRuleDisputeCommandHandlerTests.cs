using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for <see cref="SubmitRuleDisputeCommandHandler"/>.
/// Game Night Improvvisata — E3: Arbitro mode.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SubmitRuleDisputeCommandHandlerTests
{
    // ─── Fixtures ────────────────────────────────────────────────────────────

    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private const string DefaultLlmResponse =
        "VERDETTO: Il giocatore A ha ragione. Si possono giocare 2 carte per turno.\n" +
        "REGOLA: Manuale pag. 12, sezione \"Azioni di turno\": ogni giocatore può giocare fino a 2 carte.\n" +
        "NOTA: Se ci sono dubbi, consultare la FAQ ufficiale.";

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly SubmitRuleDisputeCommandHandler _sut;

    public SubmitRuleDisputeCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _tierEnforcementMock = new Mock<ITierEnforcementService>();
        _publisherMock = new Mock<IPublisher>();

        // Default: LLM always succeeds with structured response
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultLlmResponse));

        // Default: user is within quota
        _tierEnforcementMock
            .Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tierEnforcementMock
            .Setup(t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SubmitRuleDisputeCommandHandler(
            _sessionRepoMock.Object,
            _dbContext,
            _llmServiceMock.Object,
            _tierEnforcementMock.Object,
            _publisherMock.Object,
            TimeProvider.System,
            NullLogger<SubmitRuleDisputeCommandHandler>.Instance);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private LiveGameSession CreateInProgressSession(
        Guid? id = null,
        string gameName = "Wingspan",
        bool withDbEntity = false)
    {
        var sessionId = id ?? TestSessionId;
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: TestUserId,
            gameName: gameName,
            timeProvider: TimeProvider.System);

        session.AddPlayer(
            userId: TestUserId,
            displayName: "Alice",
            color: PlayerColor.Red,
            timeProvider: TimeProvider.System,
            role: PlayerRole.Host);

        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        if (withDbEntity)
        {
            _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
            {
                Id = sessionId,
                SessionCode = session.SessionCode,
                GameName = gameName,
                CreatedByUserId = TestUserId,
                Status = (int)LiveSessionStatus.InProgress,
                CurrentTurnIndex = session.CurrentTurnIndex,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AgentMode = (int)AgentSessionMode.None,
                ScoringConfigJson = "{}",
                RowVersion = new byte[] { 1 }
            });
            _dbContext.SaveChanges();
        }

        return session;
    }

    private static SubmitRuleDisputeCommand BuildCommand(
        Guid? sessionId = null,
        Guid? callerUserId = null,
        string description = "Posso giocare 2 carte per turno?",
        string player = "Alice")
        => new(
            SessionId: sessionId ?? TestSessionId,
            CallerUserId: callerUserId ?? TestUserId,
            Description: description,
            RaisedByPlayerName: player);

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ReturnsRuleDisputeResponse()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        Assert.False(string.IsNullOrWhiteSpace(result.Verdict));
        Assert.NotNull(result.RuleReferences);
        Assert.NotEmpty(result.RuleReferences);
    }

    [Fact]
    public async Task Handle_HappyPath_AddsDisputeToSession()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand(description: "Posso usare un'azione speciale due volte?");

        // Capture the session passed to UpdateAsync
        LiveGameSession? updatedSession = null;
        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => updatedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(updatedSession);
        updatedSession!.Disputes.Should().ContainSingle();
        updatedSession.Disputes[0].Description.Should().Be(command.Description);
        updatedSession.Disputes[0].RaisedByPlayerName.Should().Be(command.RaisedByPlayerName);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishesDisputeResolvedEvent()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<DisputeResolvedEvent>(e =>
                    e.SessionId == TestSessionId &&
                    e.Dispute.RaisedByPlayerName == command.RaisedByPlayerName),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_CallsLlmWithArbitrationPrompt()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand(description: "Chi va per primo al turno 3?");

        string? capturedSystemPrompt = null;
        string? capturedUserPrompt = null;

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>(
                (sys, usr, _, _) =>
                {
                    capturedSystemPrompt = sys;
                    capturedUserPrompt = usr;
                })
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultLlmResponse));

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — prompt must contain the arbitration markers
        Assert.NotNull(capturedSystemPrompt);
        Assert.Contains("ARBITRO", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains(command.Description, capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("VERDETTO:", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("REGOLA:", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("NOTA:", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Wingspan", capturedUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_HappyPath_IncludesPreviousDisputesInPrompt()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true);

        // Pre-populate two disputes on the in-memory session
        session.AddDispute(new RuleDisputeEntry(
            Guid.NewGuid(), "Prima disputa?", "Verdetto A", new List<string>(), "Bob", DateTime.UtcNow));
        session.AddDispute(new RuleDisputeEntry(
            Guid.NewGuid(), "Seconda disputa?", "Verdetto B", new List<string>(), "Carol", DateTime.UtcNow));

        var command = BuildCommand(description: "Terza domanda?");

        string? capturedUserPrompt = null;
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>(
                (_, usr, _, _) => capturedUserPrompt = usr)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultLlmResponse));

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("Prima disputa?", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Seconda disputa?", capturedUserPrompt, StringComparison.Ordinal);
        Assert.Contains("Verdetti precedenti", capturedUserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_HappyPath_ParsesVerdictFromLlmResponse()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — verdict text extracted from "VERDETTO:" section
        Assert.Contains("ragione", result.Verdict, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_HappyPath_ParsesRuleReferencesFromLlmResponse()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — REGOLA section content ends up in RuleReferences
        Assert.NotEmpty(result.RuleReferences);
        Assert.Contains(result.RuleReferences, r => r.Contains("pag.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_HappyPath_ParsesNoteFromLlmResponse()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — NOTA section present in response fixture
        Assert.NotNull(result.Note);
        Assert.Contains("FAQ", result.Note, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFoundException()
    {
        // Arrange — repository returns null
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionIsNotInProgress_ThrowsConflictException()
    {
        // Arrange — session in Created status (not started)
        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionIsPaused_ThrowsConflictException()
    {
        // Arrange — paused session
        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        session.AddPlayer(TestUserId, "Host", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        session.Pause(TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenLlmFails_ThrowsInvalidOperationException()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM unavailable"));

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    // ─── Tier enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionQueryQuotaExceeded_ThrowsConflictException()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);

        _tierEnforcementMock
            .Setup(t => t.CanPerformAsync(TestUserId, TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _tierEnforcementMock
            .Setup(t => t.GetUsageAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageSnapshot(
                PrivateGames: 0, PrivateGamesMax: 3,
                PdfThisMonth: 0, PdfThisMonthMax: 3,
                AgentQueriesToday: 0, AgentQueriesTodayMax: 20,
                SessionQueries: 30, SessionQueriesMax: 30,
                Agents: 0, AgentsMax: 1,
                PhotosThisSession: 0, PhotosThisSessionMax: 5,
                SessionSaveEnabled: false,
                CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 1));

        var command = BuildCommand();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));

        Assert.Contains("limite di domande", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30/30", ex.Message, StringComparison.Ordinal);

        // LLM must not be called when quota is exceeded
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWithinSessionQueryQuota_SucceedsAndRecordsUsage()
    {
        // Arrange — default mock allows the action
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — verdict returned and usage recorded
        result.Id.Should().NotBe(Guid.Empty);
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(TestUserId, TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Parsing edge cases ───────────────────────────────────────────────────

    [Fact]
    public void ParseArbitrationResponse_WithWellFormedResponse_ParsesAllSections()
    {
        // Arrange
        const string response =
            "VERDETTO: Giocatore A ha ragione.\n" +
            "REGOLA: Pag. 5, sezione Turni.\n" +
            "NOTA: In caso di dubbio usare la FAQ.";

        // Act
        var (verdict, refs, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert
        Assert.Contains("Giocatore A", verdict, StringComparison.Ordinal);
        Assert.NotEmpty(refs);
        Assert.Contains("Pag. 5", refs[0], StringComparison.Ordinal);
        Assert.NotNull(note);
        Assert.Contains("FAQ", note, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseArbitrationResponse_WithMalformedResponse_UsesFullResponseAsVerdict()
    {
        // Arrange
        const string response = "Il giocatore A ha torto perché le regole dicono altro.";

        // Act
        var (verdict, refs, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert — fallback: full response as verdict
        verdict.Should().Be(response);
        Assert.Empty(refs);
        Assert.Null(note);
    }

    [Fact]
    public void ParseArbitrationResponse_WithEmptyResponse_ReturnsDefaultVerdict()
    {
        // Act
        var (verdict, refs, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(string.Empty);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(verdict));
        Assert.Empty(refs);
        Assert.Null(note);
    }

    [Fact]
    public void ParseArbitrationResponse_WithMultipleRuleReferencesViaSemicolon_ParsesAllRefs()
    {
        // Arrange
        const string response =
            "VERDETTO: Ha ragione il difensore.\n" +
            "REGOLA: Pag. 3 sezione A; Pag. 7 sezione B; Errata 2024.\n" +
            "NOTA: Consultare la versione aggiornata.";

        // Act
        var (_, refs, _) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert
        Assert.True(refs.Count >= 2, $"Expected at least 2 references, got {refs.Count}");
    }

    [Fact]
    public void ParseArbitrationResponse_WithNoNota_ReturnsNullNote()
    {
        // Arrange
        const string response =
            "VERDETTO: Il giocatore B ha ragione.\n" +
            "REGOLA: Manuale pag. 8, regola delle azioni.";

        // Act
        var (verdict, refs, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert
        Assert.Contains("giocatore B", verdict, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(refs);
        Assert.Null(note);
    }
}

/// <summary>
/// Validation tests for <see cref="SubmitRuleDisputeCommandValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SubmitRuleDisputeCommandValidatorTests
{
    private readonly SubmitRuleDisputeCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Posso giocare 2 carte?", "Alice");
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.Empty, Guid.NewGuid(), "Description", "Player");
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_WithEmptyCallerUserId_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.Empty, "Description", "Player");
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CallerUserId);
    }

    [Fact]
    public void Validate_WithEmptyDescription_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), string.Empty, "Player");
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithDescriptionExceeding1000Chars_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('A', 1001), "Player");
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithEmptyPlayerName_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Description", string.Empty);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }

    [Fact]
    public void Validate_WithPlayerNameExceeding100Chars_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Description", new string('B', 101));
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }

    [Fact]
    public void Validate_WithAllFieldsEmpty_FailsAllFields()
    {
        var command = new SubmitRuleDisputeCommand(Guid.Empty, Guid.Empty, string.Empty, string.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
        result.ShouldHaveValidationErrorFor(x => x.CallerUserId);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }
}
