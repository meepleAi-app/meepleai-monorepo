using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.UserLibrary;
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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
/// <remarks>
/// In the same collection as the files that build <c>RagPromptAssemblyService</c>: the two tests of the time
/// budget record a retrieval fallback on <c>meepleai.rag.retrieval.fallbacks</c>, the process-wide counter that
/// <c>RagPromptAssemblyServiceFallbackMetricsTests</c> listens to and expects to see move once (#3994).
/// </remarks>
[Collection("RetrievalEmptyMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SubmitRuleDisputeCommandHandlerTests
{
    // ─── Fixtures ────────────────────────────────────────────────────────────

    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private static readonly Guid TestGameId = Guid.NewGuid();

    /// <summary>A realistic answer: the model says the passages cover the case, then rules.</summary>
    private const string DefaultLlmResponse =
        "COPERTURA: SI\n" +
        "VERDETTO: Il giocatore A ha ragione. Si possono giocare 2 carte per turno.\n" +
        "NOTA: Se ci sono dubbi, consultare la FAQ ufficiale.";

    /// <summary>A text the Protected rulebook holds word for word — it must never reach the table.</summary>
    private const string ProtectedVerbatim = "ogni giocatore puo' giocare fino a due carte durante il proprio turno";

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IAnswerPreconditions> _preconditionsMock;
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly SubmitRuleDisputeCommandHandler _sut;

    public SubmitRuleDisputeCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _llmServiceMock = new Mock<ILlmService>();
        _mediatorMock = new Mock<IMediator>();
        _preconditionsMock = new Mock<IAnswerPreconditions>();
        _tierEnforcementMock = new Mock<ITierEnforcementService>();
        _publisherMock = new Mock<IPublisher>();

        // Default: the RAG ownership gate lets the caller through
        _preconditionsMock
            .Setup(p => p.EnsureCanAskAboutGameAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

        // Default: KnowledgeBase believes every page it is asked about (#4782)
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid>)Array.Empty<Guid>());

        _sut = BuildHandler(preconditions: _preconditionsMock.Object);
    }

    private SubmitRuleDisputeCommandHandler BuildHandler(
        IAnswerPreconditions preconditions,
        TimeProvider? timeProvider = null,
        int arbiterBudgetMs = 15_000)
        => new(
            _sessionRepoMock.Object,
            _dbContext,
            _llmServiceMock.Object,
            _mediatorMock.Object,
            preconditions,
            _tierEnforcementMock.Object,
            _publisherMock.Object,
            Options.Create(new SessionAgentOptions { ArbiterBudgetMs = arbiterBudgetMs }),
            timeProvider ?? TimeProvider.System,
            NullLogger<SubmitRuleDisputeCommandHandler>.Instance);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static GroundedSessionAnswerDto GroundedAnswer(
        string answer,
        IReadOnlyList<CitationDto>? citations = null,
        GroundingStatus? grounding = null,
        bool retrievalFailed = false,
        bool copyrightSanitized = false)
    {
        var list = citations ?? Array.Empty<CitationDto>();
        return new GroundedSessionAnswerDto(
            answer,
            list,
            grounding ?? (list.Count > 0 ? GroundingStatus.Grounded : GroundingStatus.Ungrounded),
            Confidence: null,
            RetrievalFailed: retrievalFailed,
            CopyrightSanitized: copyrightSanitized);
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
        string player = "Alice",
        string? role = null)
        => new(
            SessionId: sessionId ?? TestSessionId,
            CallerUserId: callerUserId ?? TestUserId,
            Description: description,
            RaisedByPlayerName: player,
            CallerRole: role);

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
        sent.ResponseInstructions.Should().Contain("COPERTURA:", "the model declares on a line of its own whether the passages cover the case");
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
                "COPERTURA: SI\n" +
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
                DefaultLlmResponse, new[] { FullCitation(7, "Ogni giocatore può\ngiocare  fino a\r\ndue carte.") }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        var reference = result.RuleReferences.Should().ContainSingle().Which;
        reference.Should().Contain("7");
        reference.Should().Contain("Ogni giocatore può giocare fino a due carte.");
    }

    [Fact]
    public async Task Handle_ManyPassages_KeepsTheFirstOnesInTheOrderTheyAreHandedOver_OnceEach()
    {
        // Arrange — the handler does not sort: the order is the one KnowledgeBase hands the citations over in.
        // The scores here are not sorted, so a sort by them would put page 9 first. Page 3 comes twice.
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[]
            {
                new CitationDto("doc-rules", 8, 0.20f, null, "protected"),
                new CitationDto("doc-rules", 3, 0.60f, null, "protected"),
                new CitationDto("doc-rules", 3, 0.70f, null, "protected"),
                new CitationDto("doc-rules", 5, 0.30f, null, "protected"),
                new CitationDto("doc-rules", 9, 0.95f, null, "protected"),
            }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().Equal("Fonte, p. 8", "Fonte, p. 3", "Fonte, p. 5");
    }

    // ─── #4782: better no page than a false one ───────────────────────────────

    [Fact]
    public async Task Handle_APassageOfADocumentWhosePagesAreNotBelieved_IsCitedWithoutAPage_TheOthersKeepTheirs()
    {
        // Arrange — the flat document was indexed without the page of each passage, so it claims page 1 for all
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var flat = Guid.NewGuid();
        var withPages = Guid.NewGuid();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[]
            {
                ProtectedCitation(1, document: flat.ToString()),
                ProtectedCitation(5, document: withPages.ToString()),
            }));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid>)new[] { flat });

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().Equal("Fonte", "Fonte, p. 5");
        session.Disputes.Should().ContainSingle().Which.RuleReferences.Should().Equal("Fonte", "Fonte, p. 5");
    }

    [Fact]
    public async Task Handle_AsksKnowledgeBaseOnlyAboutTheDocumentsThatNameAPage_ByTheirGuid()
    {
        // Arrange — a passage with no page (0) has nothing to doubt, and an id that is not a GUID is not one KB wrote
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var naming = Guid.NewGuid();
        var withoutPage = Guid.NewGuid();
        GetDocumentsWithUnreliablePageNumbersQuery? asked = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[]
            {
                ProtectedCitation(3, document: naming.ToString()),
                ProtectedCitation(7, document: naming.ToString().ToUpperInvariant()),
                ProtectedCitation(0, document: withoutPage.ToString()),
                ProtectedCitation(2, document: "doc-rules"),
            }));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<IReadOnlyList<Guid>>, CancellationToken>((q, _) => asked = (GetDocumentsWithUnreliablePageNumbersQuery)q)
            .ReturnsAsync((IReadOnlyList<Guid>)Array.Empty<Guid>());

        // Act
        await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        asked.Should().NotBeNull();
        asked!.DocumentIds.Should().Equal(naming);
    }

    [Fact]
    public async Task Handle_TheSameDocumentWrittenInCapitals_IsStillTheDocumentKnowledgeBaseMeant()
    {
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var flat = Guid.NewGuid();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[]
            {
                ProtectedCitation(1, document: flat.ToString().ToUpperInvariant()),
                ProtectedCitation(7, document: flat.ToString()),
            }));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Guid>)new[] { flat });

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Both spellings are the one document: neither keeps a page.
        result.RuleReferences.Should().Equal("Fonte");
    }

    [Fact]
    public async Task Handle_CitationsWithoutAGuidDocument_NeverAskKnowledgeBase_AndKeepTheirPages()
    {
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(12) }));

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        result.RuleReferences.Should().Equal("Fonte, p. 12");
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_IfKnowledgeBaseCannotSayWhichPagesToBelieve_NoPageIsCited_AndTheVerdictIsStillGiven()
    {
        // Arrange — the pages are an extra: a failed check must not fail the verdict, nor let a doubtful page through
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var document = Guid.NewGuid().ToString();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(4, document: document) }));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Verdict.Should().Contain("Il giocatore A ha ragione");
        result.RuleReferences.Should().Equal("Fonte");
        session.Disputes.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ACancellationWhileCheckingThePages_Propagates()
    {
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        using var cts = new CancellationTokenSource();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(4, document: Guid.NewGuid().ToString()) }));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<IReadOnlyList<Guid>>, CancellationToken>((_, _) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var act = async () => await _sut.Handle(BuildCommand(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_AVerdictThatDoesNotRestOnTheRulebook_CitesNothing_AndAsksNothingAboutPages()
    {
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                "VERDETTO: Questa informazione non è disponibile nel regolamento fornito.",
                new[] { ProtectedCitation(12, document: Guid.NewGuid().ToString()) },
                GroundingStatus.Ungrounded));

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        result.RuleReferences.Should().BeEmpty();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NoPassagesNotice);
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
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NotCoveredNotice);
    }

    [Fact]
    public async Task Handle_WithoutACatalogGame_AsksTheModelDirectly_AndSaysThereIsNoRulebook()
    {
        // Arrange — an improvvisata session has no game id, so there never was a rulebook to look in
        var session = CreateInProgressSession(withDbEntity: true);

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert — the notice says what is true here: there is no rulebook. It used to say «nessun passaggio
        // pertinente», as if a search had been made and had found nothing.
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NoGameNotice);
        result.Verdict.Should().NotContain("passaggio pertinente").And.NotContain("passaggi pertinenti");
        session.Disputes.Should().ContainSingle();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _preconditionsMock.Verify(
            p => p.EnsureCanAskAboutGameAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
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
                "COPERTURA: SI\nVERDETTO: [ref:doc-rules:12] Il giocatore A ha ragione.\nNOTA: [ref:doc-rules:13] Vale lo stesso per tutti.",
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

    // ─── #4751: the RAG ownership gate (ADR-096) protects the retrieval ────────

    [Fact]
    public async Task Handle_WhenTheGateDenies_NothingIsRetrieved_AndTheVerdictSaysOwnershipIsNotDeclared()
    {
        // Arrange — the copyright tier protects the ANSWER; the gate protects the RETRIEVAL. Without it a manual
        // the caller may not read would go into the prompt, and the only defence would be what the model writes.
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _preconditionsMock
            .Setup(p => p.EnsureCanAskAboutGameAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagOwnershipRequiredException(TestGameId));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert — not a 403: the verdict is given without the rulebook, and says why
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.OwnershipNotDeclaredNotice);
        result.Verdict.Should().Contain("possesso del gioco non dichiarato");
        result.Verdict.Should().Contain("nessun riferimento è citato");
        session.Disputes.Should().ContainSingle();
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AsksTheGateForTheCaller_WithTheRoleTheCommandCarries()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);

        // Act
        await _sut.Handle(BuildCommand(role: "Admin"), CancellationToken.None);

        // Assert — whoever raised the dispute, at the single point every answering path uses
        _preconditionsMock.Verify(
            p => p.EnsureCanAskAboutGameAsync(TestUserId, "Admin", TestGameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PrivateGameOfSomeoneElse_IsADenial_LikeAnyOther()
    {
        // Arrange — PrivateGameHub creates the session with gameId = the private game id, so a guest at the
        // table could otherwise draw from the host's private PDF. The REAL gate here, over an in-memory database.
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _dbContext.PrivateGames.Add(PrivateGame(TestGameId, ownerId: Guid.NewGuid()));
        _dbContext.SaveChanges();
        var sut = BuildHandler(RealGate());

        // Act
        var result = await sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.OwnershipNotDeclaredNotice);
        result.RuleReferences.Should().BeEmpty();
        session.Disputes.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_OwnPrivateGame_PassesTheRealGate()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _dbContext.PrivateGames.Add(PrivateGame(TestGameId, ownerId: TestUserId));
        _dbContext.SaveChanges();
        var sut = BuildHandler(RealGate());

        // Act
        await sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("User", false)]
    [InlineData(null, false)]
    [InlineData("not-a-role", false)]
    public async Task Handle_TheRealGate_LetsAnAdministratorThroughWithoutOwnership_AndNobodyElse(
        string? role, bool passes)
    {
        // Arrange — the game belongs to no one and is not public: only the role can open it. An unknown role
        // or none counts as a plain user (when in doubt the gate checks).
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var sut = BuildHandler(RealGate());

        // Act
        var result = await sut.Handle(BuildCommand(role: role), CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()),
            passes ? Times.Once : Times.Never);
        if (!passes)
        {
            result.Verdict.Should().StartWith(ArbiterVerdictFormat.OwnershipNotDeclaredNotice);
        }
    }

    private IAnswerPreconditions RealGate()
        => new AnswerPreconditions(
            new RagAccessService(_dbContext),
            _tierEnforcementMock.Object,
            NullLogger<AnswerPreconditions>.Instance);

    private static PrivateGameEntity PrivateGame(Guid id, Guid ownerId) => new()
    {
        Id = id,
        OwnerId = ownerId,
        Title = $"PrivateGame-{id:N}",
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow,
    };

    // ─── #4751: the model declares whether the passages cover the case ────────

    [Theory]
    [InlineData("COPERTURA: SI")]
    [InlineData("COPERTURA: Sì")]
    [InlineData("**COPERTURA:** SI")]
    [InlineData("## COPERTURA: si.")]
    public async Task Handle_WhenTheModelSaysThePassagesCoverTheCase_CitesThem(string coverageLine)
    {
        // Arrange — a realistic answer, through the parser: no state is built by hand
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                $"{coverageLine}\nVERDETTO: Ha ragione Sara.\nNOTA: Nessuna.", new[] { ProtectedCitation(7) }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().Equal("Fonte, p. 7");
        result.Verdict.Should().Be("Ha ragione Sara.");
    }

    [Theory]
    [InlineData("COPERTURA: NO")]
    [InlineData("**COPERTURA:** NO")]
    [InlineData("COPERTURA: Non coperto")]
    [InlineData("COPERTURA: forse")]
    [InlineData("COPERTURA: Sì, ma solo in parte")]
    [InlineData("COPERTURA: SI/NO")]
    [InlineData("COPERTURA: SI\nCOPERTURA: NO")]
    [InlineData("COPERTURA: [SI se i passaggi coprono il caso; NO se non lo coprono]")]
    [InlineData("COPERTURA:")]
    [InlineData(null)]
    public async Task Handle_WhenTheModelSaysNoOrDoesNotSay_DeclaresItAndCitesNothing(string? coverageLine)
    {
        // Arrange — the prompt tells the model to say, on a line of its own, whether the passages cover the
        // case; it will not emit the fixed refusal sentence, so grounding alone would never see this. «No», an
        // answer that is not a clear «yes», and no line at all all land on the same side (fail-honest).
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var answer = (coverageLine is null ? string.Empty : coverageLine + "\n") +
                     "VERDETTO: I passaggi non coprono il caso; l'interpretazione più ragionevole è che Sara abbia ragione.\n" +
                     "NOTA: Interpretazione, non regola scritta.";
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(answer, new[] { ProtectedCitation(7), FullCitation(9, "Testo del passaggio.") }));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NotCoveredNotice);
        result.Verdict.Should().Contain("l'interpretazione più ragionevole");
        session.Disputes.Should().ContainSingle().Which.RuleReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheRetrievalFailed_SaysTheRulebookCouldNotBeConsulted_NotThatNothingWasFound()
    {
        // Arrange — a failure and an empty result both leave no passage; they are not the same thing, and
        // «nessun passaggio pertinente» would be false for the first
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, citations: null, retrievalFailed: true));

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.RetrievalUnavailableNotice);
        result.Verdict.Should().NotContain("passaggi pertinenti");
        result.RuleReferences.Should().BeEmpty();
    }

    // ─── #4751: the leak guard's fallback message is not a verdict ────────────

    private void TheGroundedAnswerIsReplacedByTheLeakGuard()
        => _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(
                "Non posso riportare il testo protetto del regolamento.",
                new[] { ProtectedCitation(12) },
                copyrightSanitized: true));

    [Fact]
    public async Task Handle_WhenTheLeakGuardReplacedTheAnswer_TheVerdictIsGivenOnceWithoutTheRulebook_AndSaysSo()
    {
        // Arrange — the answer repeated a run of a Protected passage and GroundedAnswerService replaced all of it
        // with the copyright message. That message is not a verdict, and it must not be saved as one. Failing
        // instead would trap a player whose dispute makes the model repeat the text (one who pastes a passage of
        // the rulebook into it) in an error on every attempt, at two model calls each.
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        TheGroundedAnswerIsReplacedByTheLeakGuard();

        // Act
        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert — a verdict without the rulebook, with the reason; the fallback message is nowhere in it
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.CopyrightWithheldNotice);
        result.Verdict.Should().Contain("Il giocatore A ha ragione");
        result.Verdict.Should().NotContain("Non posso riportare il testo protetto");
        result.RuleReferences.Should().BeEmpty();
        session.Disputes.Should().ContainSingle().Which.Verdict.Should().Be(result.Verdict);

        // Once: the retrieval is not asked again, the quota is charged for one verdict, the table gets one event
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _tierEnforcementMock.Verify(
            t => t.RecordUsageAsync(TestUserId, TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()),
            Times.Once);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<DisputeResolvedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheLeakGuardReplacedTheAnswer_TheSecondAttemptHasNothingOfTheRulebookInIt()
    {
        // The second model call is the direct one: no passage, no retrieval — only the dispute and the session
        string? systemPrompt = null;
        string? userPrompt = null;
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        TheGroundedAnswerIsReplacedByTheLeakGuard();
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((system, user, _, _) =>
            {
                systemPrompt = system;
                userPrompt = user;
            })
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(DefaultLlmResponse));

        await _sut.Handle(BuildCommand(description: "Chi va per primo al turno 3?"), CancellationToken.None);

        systemPrompt.Should().ContainEquivalentOf("non hai accesso al regolamento");
        userPrompt.Should().Contain("Chi va per primo al turno 3?");
        (systemPrompt + userPrompt).Should().NotContain("SYS-BASE").And.NotContain("COPERTURA");
    }

    [Fact]
    public async Task Handle_WhenTheLeakGuardReplacedTheAnswer_AndTheSecondAttemptFails_NothingIsSavedChargedOrPublished()
    {
        // Arrange — the failure semantics of #4764 apply to the branch that replaces the first attempt
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        TheGroundedAnswerIsReplacedByTheLeakGuard();
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM unavailable"));

        // Act
        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        // Assert
        ex.StatusCode.Should().Be(503);
        ex.ErrorCode.Should().Be(SubmitRuleDisputeCommandHandler.NoVerdictErrorCode);
        ex.Message.Should().NotContain("LLM unavailable").And.NotContain("protetto");
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
    public async Task Handle_WhenTheLeakGuardReplacedTheAnswer_AndTheGatewayRefusesTheSecondAttempt_PassesItsCodeOn()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        TheGroundedAnswerIsReplacedByTheLeakGuard();
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Il tetto di spesa giornaliero è stato raggiunto.") with
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
        ex.ErrorCode.Should().Be("AI_DAILY_BUDGET_EXCEEDED");
        session.Disputes.Should().BeEmpty();
    }

    // ─── #4751: a time budget on the grounded answer ──────────────────────────

    [Fact]
    public async Task Handle_WhenTheBudgetExpires_TheVerdictGoesWithoutTheRulebook_AndSaysSo()
    {
        // Arrange — the retrieval never answers: only the budget (on a controlled clock) ends the wait
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var clock = new FakeTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentToken = CancellationToken.None;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<GroundedSessionAnswerDto>, CancellationToken>(async (_, ct) =>
            {
                sentToken = ct;
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return GroundedAnswer("unreachable");
            });
        var sut = BuildHandler(_preconditionsMock.Object, clock, arbiterBudgetMs: 5_000);

        // Act
        var pending = sut.Handle(BuildCommand(), CancellationToken.None);
        await started.Task;
        clock.Advance(TimeSpan.FromMilliseconds(4_999));
        var firedTooEarly = sentToken.IsCancellationRequested;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        firedTooEarly.Should().BeFalse("the budget is 5000 ms");
        sentToken.IsCancellationRequested.Should().BeTrue();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.RetrievalUnavailableNotice);
        result.RuleReferences.Should().BeEmpty();
        session.Disputes.Should().ContainSingle();
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAGenerationFailsBecauseTheBudgetCutIt_TheVerdictStillGoesWithoutTheRulebook()
    {
        // Arrange — some clients report a cancelled call as a failed one: the failure is the budget's, not the provider's
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        var clock = new FakeTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<GroundedSessionAnswerDto>, CancellationToken>(async (_, ct) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // reported as a failure, not rethrown
                }

                throw new GroundedSessionGenerationException("generation cancelled");
            });
        var sut = BuildHandler(_preconditionsMock.Object, clock, arbiterBudgetMs: 5_000);

        // Act
        var pending = sut.Handle(BuildCommand(), CancellationToken.None);
        await started.Task;
        clock.Advance(TimeSpan.FromMilliseconds(5_000));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.RetrievalUnavailableNotice);
    }

    [Fact]
    public async Task Handle_WhenTheCallerCancels_ItIsNotAnExpiredBudget()
    {
        // Arrange — a client that goes away is not a slow retrieval: no second call, nothing saved
        var session = CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        using var callerCancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<GroundedSessionAnswerDto>, CancellationToken>(async (_, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return GroundedAnswer("unreachable");
            });

        // Act
        var pending = _sut.Handle(BuildCommand(), callerCancellation.Token);
        await started.Task;
        await callerCancellation.CancelAsync();
        var act = () => pending.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        session.Disputes.Should().BeEmpty();
        _llmServiceMock.Verify(
            s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTheBudgetDisabled_StillAsksTheRulebook()
    {
        // Arrange — 0 or less turns the budget off
        CreateInProgressSession(withDbEntity: true, gameId: TestGameId);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AskGroundedSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroundedAnswer(DefaultLlmResponse, new[] { ProtectedCitation(12) }));
        var sut = BuildHandler(_preconditionsMock.Object, arbiterBudgetMs: 0);

        // Act
        var result = await sut.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.RuleReferences.Should().Equal("Fonte, p. 12");
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
            Guid.NewGuid(), Guid.NewGuid(), "Posso giocare 2 carte?", "Alice", null);
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.Empty, Guid.NewGuid(), "Description", "Player", null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_WithEmptyCallerUserId_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.Empty, "Description", "Player", null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CallerUserId);
    }

    [Fact]
    public void Validate_WithEmptyDescription_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), string.Empty, "Player", null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithDescriptionExceeding1000Chars_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('A', 1001), "Player", null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithEmptyPlayerName_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Description", string.Empty, null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }

    [Fact]
    public void Validate_WithPlayerNameExceeding100Chars_Fails()
    {
        var command = new SubmitRuleDisputeCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Description", new string('B', 101), null);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }

    [Fact]
    public void Validate_WithAllFieldsEmpty_FailsAllFields()
    {
        var command = new SubmitRuleDisputeCommand(Guid.Empty, Guid.Empty, string.Empty, string.Empty, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
        result.ShouldHaveValidationErrorFor(x => x.CallerUserId);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.RaisedByPlayerName);
    }
}
