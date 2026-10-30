using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using CitationDto = Api.Models.CitationDto;
using KbCopyrightTier = Api.BoundedContexts.KnowledgeBase.Domain.Enums.CopyrightTier;

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

    private static readonly Guid TestGameId = Guid.NewGuid();

    private const string DefaultLlmResponse =
        "VERDETTO: Il giocatore A ha ragione. Si possono giocare 2 carte per turno.\n" +
        "NOTA: Se ci sono dubbi, consultare la FAQ ufficiale.";

    /// <summary>A text the Protected rulebook holds word for word — it must never reach the table.</summary>
    private const string ProtectedVerbatim = "ogni giocatore puo' giocare fino a due carte durante il proprio turno";

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly SubmitRuleDisputeCommandHandler _sut;

    public SubmitRuleDisputeCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _mediatorMock = new Mock<IMediator>();
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

        // Default for the grounded path: the rulebook answers with a structured verdict and no citation
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse));

        _sut = new SubmitRuleDisputeCommandHandler(
            _sessionRepoMock.Object,
            _dbContext,
            _llmServiceMock.Object,
            _mediatorMock.Object,
            _tierEnforcementMock.Object,
            _publisherMock.Object,
            TimeProvider.System,
            NullLogger<SubmitRuleDisputeCommandHandler>.Instance);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static GroundedSessionAnswerDto GroundedAnswer(
        string answer,
        IReadOnlyList<CitationDto>? citations = null,
        GroundingStatus? grounding = null)
    {
        var list = citations ?? Array.Empty<CitationDto>();
        return new GroundedSessionAnswerDto(
            answer,
            list,
            grounding ?? (list.Count > 0 ? GroundingStatus.Grounded : GroundingStatus.Ungrounded),
            Confidence: null);
    }

    /// <summary>What the shared finalize step hands over: a Full citation keeps its text, a Protected one has none.</summary>
    private static CitationDto FullCitation(int page, string snippet, string document = "doc-rules")
        => new(document, page, 0.9f, snippet, WireTier(KbCopyrightTier.Full));

    private static CitationDto ProtectedCitation(int page, string? paraphrase = null, string document = "doc-rules")
        => new(document, page, 0.9f, SnippetPreview: null, CopyrightTier: WireTier(KbCopyrightTier.Protected),
            ParaphrasedSnippet: paraphrase);

    /// <summary>The tier as KnowledgeBase writes it on the wire (<c>GroundedAnswerService</c>: the enum name, lower-cased).</summary>
    private static string WireTier(KbCopyrightTier tier) => tier.ToString().ToLowerInvariant();

    private LiveGameSession CreateInProgressSession(
        Guid? id = null,
        string gameName = "Wingspan",
        bool withDbEntity = false,
        Guid? gameId = null)
    {
        var sessionId = id ?? TestSessionId;
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: TestUserId,
            gameName: gameName,
            timeProvider: TimeProvider.System,
            gameId: gameId);

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
                Xmin = 1u
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
        // Arrange — the session has a catalog game, so the verdict starts from its rulebook
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(12) }));
        var command = BuildCommand();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Verdict.Should().ContainEquivalentOf("ragione");
        result.RuleReferences.Should().NotBeEmpty();
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
        updatedSession.Should().NotBeNull();
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
        capturedSystemPrompt.Should().NotBeNull();
        capturedSystemPrompt.Should().ContainEquivalentOf("ARBITRO");
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain(command.Description);
        capturedUserPrompt.Should().Contain("VERDETTO:");
        capturedUserPrompt.Should().Contain("NOTA:");
        capturedUserPrompt.Should().Contain("Wingspan");

        // #4751: no rulebook here, so the model is not asked for a quote it cannot have — and is told so
        (capturedSystemPrompt + capturedUserPrompt).Should().NotContainEquivalentOf("citazione esatta");
        (capturedSystemPrompt + capturedUserPrompt).Should().NotContain("REGOLA:");
        capturedSystemPrompt.Should().ContainEquivalentOf("non hai accesso al regolamento");
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
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("Prima disputa?");
        capturedUserPrompt.Should().Contain("Seconda disputa?");
        capturedUserPrompt.Should().Contain("Verdetti precedenti");
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
        result.Verdict.Should().ContainEquivalentOf("ragione");
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
        result.Note.Should().NotBeNull();
        result.Note.Should().ContainEquivalentOf("FAQ");
    }

    // ─── #4751: the verdict starts from the rulebook, at the most restrictive copyright tier ─────

    [Fact]
    public async Task Handle_AsksTheGroundedPath_WithTheMostRestrictiveTier_AndTheDisputeAsTheQuestion()
    {
        // Arrange — the caller is the host and may well own the game: the verdict is saved and sent to
        // the whole table, so it is written at the level of someone who owns nothing
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        AskGroundedSessionQuery? sent = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<GroundedSessionAnswerDto>, CancellationToken>((q, _) => sent = (AskGroundedSessionQuery)q)
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(12) }));
        var command = BuildCommand(description: "Chi va per primo al turno 3?");

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        sent.Should().NotBeNull();
        sent!.GameId.Should().Be(TestGameId);
        sent.UserId.Should().Be(Guid.Empty, "a verdict shared with the table must not be written at the caller's tier");
        sent.UserId.Should().NotBe(TestUserId);
        sent.AgentLanguage.Should().Be("it");
        sent.Question.Should().Be("Chi va per primo al turno 3?", "the dispute alone is the retrieval query");
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PutsThePersonaAndTheFormatInTheInstructions_NotInTheRetrievalQuery()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        session.AddDispute(new RuleDisputeEntry(
            Guid.NewGuid(), "Prima disputa?", "Verdetto A", new List<string>(), "Bob", DateTime.UtcNow));
        AskGroundedSessionQuery? sent = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<GroundedSessionAnswerDto>, CancellationToken>((q, _) => sent = (AskGroundedSessionQuery)q)
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(12) }));

        // Act
        await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert — the format is for generation only: pasted into the question it would be embedded
        sent!.Question.Should().NotContain("VERDETTO").And.NotContain("NOTA:");
        sent.ResponseInstructions.Should().NotBeNull();
        sent.ResponseInstructions.Should().ContainEquivalentOf("ARBITRO");
        sent.ResponseInstructions.Should().Contain("VERDETTO:").And.Contain("NOTA:");
        sent.ResponseInstructions.Should().Contain("Wingspan");
        sent.ResponseInstructions.Should().Contain("Verdetti precedenti").And.Contain("Prima disputa?");

        // …and it no longer asks the model for the quote it used to invent
        sent.ResponseInstructions.Should().NotContainEquivalentOf("citazione esatta");
        sent.ResponseInstructions.Should().NotContain("REGOLA:");
    }

    [Fact]
    public async Task Handle_ProtectedPassage_TheReferenceCarriesThePageAndNoText()
    {
        // Arrange — a Protected rulebook: the verdict is in the model's own words, the reference is a page.
        // The model also writes a REGOLA line, as it used to be asked to: it is a quote nobody retrieved.
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                "VERDETTO: Il giocatore A ha ragione.\n" +
                "REGOLA: Manuale pag. 99, \"testo inventato dal modello\".\n" +
                "NOTA: Nessuna.",
                new[] { ProtectedCitation(12, paraphrase: "due carte per turno") }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().ContainSingle();
        result.RuleReferences[0].Should().Contain("12");
        result.RuleReferences[0].Should().NotContain("due carte per turno", "a Protected passage has no text to show");
        string.Join(" ", result.RuleReferences).Should().NotContain("99").And.NotContain("inventato");
        result.Verdict.Should().NotContain("inventato", "the REGOLA section is not part of the verdict");
        session.Disputes.Should().ContainSingle().Which.RuleReferences.Should().Equal(result.RuleReferences);
    }

    [Fact]
    public async Task Handle_ProtectedPassageThatCarriesAPreview_NeverShowsIt()
    {
        // Arrange — the shared finalize step nulls the preview of a Protected citation. If a defect upstream
        // let one through, the verdict still must not carry it: the tier decides, not the presence of text.
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var leaked = new CitationDto("doc-rules", 12, 0.9f, ProtectedVerbatim, "protected");
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { leaked }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().ContainSingle().Which.Should().Contain("12");
        string.Join(" ", result.RuleReferences).Should().NotContain("giocatore puo'");
    }

    [Fact]
    public async Task Handle_FullPassage_TheReferenceQuotesIt()
    {
        // Arrange — free licence or an unprotected category: the verbatim text is the table's to read
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                DefaultLlmResponse, new[] { FullCitation(7, "Ogni giocatore può giocare fino a due carte.") }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        var reference = result.RuleReferences.Should().ContainSingle().Which;
        reference.Should().Contain("7");
        reference.Should().Contain("Ogni giocatore può giocare fino a due carte.");
    }

    [Fact]
    public async Task Handle_ManyPassages_KeepsTheMostRelevantPagesOnceEach()
    {
        // Arrange — five retrieved passages over four pages; two on page 3
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[]
            {
                new CitationDto("doc-rules", 3, 0.95f, null, "protected"),
                new CitationDto("doc-rules", 3, 0.90f, null, "protected"),
                new CitationDto("doc-rules", 8, 0.80f, null, "protected"),
                new CitationDto("doc-rules", 5, 0.70f, null, "protected"),
                new CitationDto("doc-rules", 9, 0.10f, null, "protected"),
            }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().HaveCount(3);
        result.RuleReferences[0].Should().Contain("3");
        result.RuleReferences[1].Should().Contain("8");
        result.RuleReferences[2].Should().Contain("5");
    }

    [Fact]
    public async Task Handle_NoPassageFound_TheVerdictSaysSo_AndCitesNothing()
    {
        // Arrange — the game has no indexed rulebook (or nothing in it matches): nothing was retrieved
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, citations: null));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert — the declaration is part of the verdict itself: it is what the table receives
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(SubmitRuleDisputeCommandHandler.NotFromRulebookNotice);
        result.Verdict.Should().Contain("Il giocatore A ha ragione");
        session.Disputes.Should().ContainSingle().Which.Verdict.Should().Be(result.Verdict);
    }

    [Fact]
    public async Task Handle_TheAnswerRefusesDespiteRetrievedPassages_IsDeclaredNotGrounded()
    {
        // Arrange — passages were found, but the answer does not rest on them (the refusal): citing
        // them as the basis of the verdict would be false
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                "VERDETTO: Questa informazione non è disponibile nel regolamento fornito.",
                new[] { ProtectedCitation(12) },
                GroundingStatus.Ungrounded));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(SubmitRuleDisputeCommandHandler.NotFromRulebookNotice);
    }

    [Fact]
    public async Task Handle_WithoutACatalogGame_AsksTheModelDirectly_AndSaysSo()
    {
        // Arrange — an improvvisata session has no game id, so there is no rulebook to retrieve from
        var session = CreateInProgressSession(withDbEntity: true);

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(SubmitRuleDisputeCommandHandler.NotFromRulebookNotice);
        session.Disputes.Should().ContainSingle();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StripsTheCopyrightMarkersFromTheVerdict()
    {
        // Arrange — for Protected sources the model is told to put [ref:doc:page] before each paraphrase;
        // the marker is machine syntax, not something to show the table
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                "VERDETTO: [ref:doc-rules:12] Il giocatore A ha ragione.\nNOTA: [ref:doc-rules:13] Vale lo stesso per tutti.",
                new[] { ProtectedCitation(12) }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Verdict.Should().Be("Il giocatore A ha ragione.");
        result.Note.Should().Be("Vale lo stesso per tutti.");
    }

    // ─── #4751: the grounded path fails like the direct one ──────────────────

    [Fact]
    public async Task Handle_GroundedPath_WhenTheGatewayRefuses_PassesItsCodeAndMessageOn_AndChargesNothing()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GroundedSessionGenerationException(
                "Il tetto di spesa giornaliero è stato raggiunto.", "AI_DAILY_BUDGET_EXCEEDED"));

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<LlmCallRefusedException>()).Which;

        // Assert
        ex.ErrorCode.Should().Be("AI_DAILY_BUDGET_EXCEEDED");
        ex.Message.Should().Be("Il tetto di spesa giornaliero è stato raggiunto.");
        session.Disputes.Should().BeEmpty();
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(LlmAnswerErrorCodes.ProviderUnavailable)]
    public async Task Handle_GroundedPath_WhenTheProviderDidNotAnswer_KeepsTheGenericMessage(string? code)
    {
        // Arrange — «empty grounded answer» carries no code; AI_PROVIDER_UNAVAILABLE is transient
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GroundedSessionGenerationException("provider-x returned 503 (secret-detail)", code));

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        // Assert
        ex.StatusCode.Should().Be(503);
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        ex.Message.Should().NotContain("secret-detail");
        session.Disputes.Should().BeEmpty();
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("VERDETTO:\nNOTA:")]
    [InlineData("[ref:doc-rules:12]")]
    public async Task Handle_GroundedPath_WhenTheAnswerHoldsNoVerdict_SavesChargesAndPublishesNothing(string empty)
    {
        // Arrange — the bare skeleton, or only a marker, is not a verdict
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(empty, new[] { ProtectedCitation(12) }));

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        // Assert
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        session.Disputes.Should().BeEmpty();
        _sessionRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<DisputeResolvedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GroundedPath_WhenSessionQueryQuotaExceeded_DoesNotRetrieveOrGenerate()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
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
                PhotosThisSession: 0, PhotosThisSessionMax: 5,
                SessionSaveEnabled: false,
                CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 1));

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
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
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
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
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenLlmFails_ThrowsAReadableServiceError_NotAServerFault()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM unavailable"));

        var command = BuildCommand();

        // Act & Assert — #4751: a 503 the player can read, not an InvalidOperationException (500)
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        ex.StatusCode.Should().Be(503);
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        ex.Message.Should().NotContain("LLM unavailable", "the model's error text belongs in the log, not in the response");
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── #4751: a refusal by the gateway says why ─────────────────────────────

    [Fact]
    public async Task Handle_WhenTheGatewayRefusesTheCall_PassesItsCodeAndMessageOn_AndChargesNothing()
    {
        // Arrange — a daily cap: «riprova tra poco» would be false, the gateway's own words are not
        var session = CreateInProgressSession(withDbEntity: true);
        var refusal = LlmCompletionResult.CreateFailure("Il tetto di spesa giornaliero è stato raggiunto.");
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refusal with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["error_code"] = "AI_DAILY_BUDGET_EXCEEDED",
                },
            });

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<LlmCallRefusedException>()).Which;

        // Assert
        ex.StatusCode.Should().Be(503);
        ex.ErrorCode.Should().Be("AI_DAILY_BUDGET_EXCEEDED");
        ex.Message.Should().Be("Il tetto di spesa giornaliero è stato raggiunto.");
        session.Disputes.Should().BeEmpty();
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoProviderAnswered_KeepsTheGenericMessage_NotTheGatewaysText()
    {
        // Arrange — AI_PROVIDER_UNAVAILABLE is transient: «riprova tra poco» is true, and the
        // gateway text may carry provider detail that belongs in the log
        CreateInProgressSession(withDbEntity: true);
        var failure = LlmCompletionResult.CreateFailure("no model answered: provider-x returned 503 (secret-detail)");
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["error_code"] = LlmAnswerErrorCodes.ProviderUnavailable,
                },
            });

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        // Assert
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        ex.Message.Should().NotContain("secret-detail");
    }

    // ─── #4751: an empty answer is not a verdict ──────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \n")]
    [InlineData("VERDETTO:\nREGOLA:\nNOTA:")]
    [InlineData("VERDETTO:")]
    public async Task Handle_WhenTheModelAnswersWithNothing_SavesChargesAndPublishesNothing(string? emptyAnswer)
    {
        // Arrange — the call "succeeds" but carries no text (or only the empty skeleton of the format)
        var session = CreateInProgressSession(withDbEntity: true);

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(emptyAnswer!));

        var command = BuildCommand();

        // Act
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        // Assert — it used to be saved as «Nessun verdetto disponibile.», broadcast and counted against the quota
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        session.Disputes.Should().BeEmpty("no verdict was given, so none is attached to the session");
        _sessionRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<DisputeResolvedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheModelAnswersWithNothing_TheDisputeStaysAskableAfterwards()
    {
        // Arrange — the first answer is empty, the retry works: the failed attempt must leave no trace
        var session = CreateInProgressSession(withDbEntity: true);

        _llmServiceMock
            .SetupSequence(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(string.Empty))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultLlmResponse));

        var command = BuildCommand();

        // Act
        var first = () => _sut.Handle(command, CancellationToken.None);
        await first.Should().ThrowAsync<ExternalServiceException>();
        var second = await _sut.Handle(command, CancellationToken.None);

        // Assert
        second.Verdict.Should().Contain("Il giocatore A ha ragione");
        session.Disputes.Should().ContainSingle("only the answered attempt is attached");
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(TestUserId, TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()),
            Times.Once);
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
                PhotosThisSession: 0, PhotosThisSessionMax: 5,
                SessionSaveEnabled: false,
                CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 1));

        var command = BuildCommand();

        // Act & Assert
        var act = () =>
            _sut.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ConflictException>()).Which;

        ex.Message.Should().ContainEquivalentOf("limite di domande");
        ex.Message.Should().Contain("30/30");

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
    public void ParseArbitrationResponse_WithWellFormedResponse_ParsesVerdictAndNote()
    {
        // Arrange
        const string response =
            "VERDETTO: Giocatore A ha ragione.\n" +
            "NOTA: In caso di dubbio usare la FAQ.";

        // Act
        var (verdict, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert
        verdict.Should().Contain("Giocatore A");
        note.Should().NotBeNull();
        note.Should().Contain("FAQ");
    }

    [Fact]
    public void ParseArbitrationResponse_ARegolaSectionFromTheModel_IsNeitherTheVerdictNorTheNote()
    {
        // #4751: the model is no longer asked for REGOLA — a quote it cannot have. If it writes one
        // anyway (habit, or the earlier prompt in a long conversation), it is dropped, not shown as the
        // text of the rulebook: the references come from the passages that were retrieved.
        const string response =
            "VERDETTO: Giocatore A ha ragione.\n" +
            "REGOLA: Pag. 5, sezione Turni: \"testo inventato\".\n" +
            "NOTA: In caso di dubbio usare la FAQ.";

        var (verdict, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        verdict.Should().Be("Giocatore A ha ragione.");
        note.Should().Be("In caso di dubbio usare la FAQ.");
    }

    [Fact]
    public void ParseArbitrationResponse_WithMalformedResponse_UsesFullResponseAsVerdict()
    {
        // Arrange
        const string response = "Il giocatore A ha torto perché le regole dicono altro.";

        // Act
        var (verdict, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert — fallback: full response as verdict
        verdict.Should().Be(response);
        note.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void ParseArbitrationResponse_WithEmptyResponse_FailsLoudlyInsteadOfInventingAVerdict(string empty)
    {
        // #4751: it used to return «Nessun verdetto disponibile.», which the handler saved as a real
        // verdict. The handler refuses an empty answer before parsing; reaching this empty is a caller bug.
        var act = () => SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("VERDETTO:\nREGOLA:\nNOTA:")]
    [InlineData("VERDETTO:")]
    [InlineData("  verdetto:   \n\nregola:  \n")]
    public void ParseArbitrationResponse_WithOnlyEmptySectionHeaders_YieldsAnEmptyVerdict(string skeleton)
    {
        var (verdict, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(skeleton);

        verdict.Should().BeEmpty("the bare skeleton is not an answer, and must not be saved as one");
        note.Should().BeNull();
    }

    [Fact]
    public void ParseArbitrationResponse_WithTextButNoVerdettoSection_KeepsTheTextAsTheVerdict()
    {
        var (verdict, _) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(
            "Ha ragione Sara: il ladro si sposta subito.");

        verdict.Should().Be("Ha ragione Sara: il ladro si sposta subito.");
    }

    [Fact]
    public void ParseArbitrationResponse_WithOnlyARegolaSection_HasNoVerdict()
    {
        // A quote is not a ruling, and it is the one thing the model cannot have
        var (verdict, _) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse("REGOLA: pag. 3, «Il ladro»");

        verdict.Should().BeEmpty();
    }

    [Fact]
    public void ParseArbitrationResponse_WithAnUnheadedVerdictAndARegolaSection_KeepsOnlyTheVerdict()
    {
        var (verdict, _) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(
            "Ha ragione Sara.\nREGOLA: pag. 3, «Il ladro»");

        verdict.Should().Be("Ha ragione Sara.");
    }

    [Fact]
    public void ParseArbitrationResponse_WithNoNota_ReturnsNullNote()
    {
        // Arrange
        const string response = "VERDETTO: Il giocatore B ha ragione.";

        // Act
        var (verdict, note) = SubmitRuleDisputeCommandHandler.ParseArbitrationResponse(response);

        // Assert
        verdict.Should().ContainEquivalentOf("giocatore B");
        note.Should().BeNull();
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
