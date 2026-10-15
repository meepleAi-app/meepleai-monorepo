using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using DomainChatMessage = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §6: un turno dell'assistente, dal comando agli eventi. Il ciclo è quello vero (la
/// catena M.E.AI di A0 con gli strumenti di A1); sono finti il gateway, la sessione e i repository.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class AssistantTurnTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid VersionId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private const string ReserveModel = "meta-llama/llama-3.3-70b-instruct:free";

    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly Mock<IPublishedAgentProfileReader> _profiles = new();
    private readonly Mock<IToolCallingModelCatalog> _catalog = new();
    private readonly Mock<IChatThreadRepository> _threads = new();
    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILlmService> _gateway = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));
    private readonly List<LlmChatRequest> _requests = [];
    private readonly List<AssistantProposal> _addedProposals = [];
    private ChatThread? _savedThread;

    public AssistantTurnTests()
    {
        Access("InProgress", accepts: true);
        _directory.Setup(d => d.CreateReader(Session, Owner)).Returns(_reader.Object);
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State());
        Publish(AgentProfileTests.Assistant());
        _catalog.Setup(c => c.GetToolCallingModelIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash", ReserveModel });
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync((ChatThread?)null);
        _threads.Setup(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Callback<ChatThread, CancellationToken>((thread, _) => _savedThread = thread).Returns(Task.CompletedTask);
        _threads.Setup(t => t.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Callback<ChatThread, CancellationToken>((thread, _) => _savedThread = thread).Returns(Task.CompletedTask);
        _proposals.Setup(p => p.AddAsync(It.IsAny<AssistantProposal>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantProposal, CancellationToken>((proposal, _) => _addedProposals.Add(proposal)).Returns(Task.CompletedTask);
        _proposals.Setup(p => p.ListByThreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _proposals.Setup(p => p.ListOpenBySessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AssistantSessionState State() => new(
        GameId: null,
        GameName: "Azul",
        Status: "InProgress",
        CanRecordScores: true,
        CanAdvanceTurn: true,
        Players: [new(Marco, "Marco", 7, true), new(Giulia, "Giulia", 12, true)],
        CurrentTurnPlayerId: Giulia,
        CurrentTurnIndex: 4,
        CurrentRound: 2,
        CurrentPhase: null,
        Dimensions: ["punti"],
        Scores: [new(Marco, 2, "punti", 7)],
        HouseRules: []);

    private void Access(string status, bool accepts) =>
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, status, accepts));

    /// <summary>Il contenuto di prova ha «en» come lingua di riserva e deepseek-flash come modello.</summary>
    private void Publish(SessionAssistantSettings assistant) =>
        _profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishedAgentProfile(VersionId, 3, AgentProfileTests.Content(model: "deepseek-flash", assistant: assistant)));

    private void Script(params LlmCompletionResult[] turns)
    {
        var queue = new Queue<LlmCompletionResult>(turns);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : LlmCompletionResult.CreateFailure("script finished"));
    }

    private static LlmCompletionResult Calls(string tool, string argumentsJson) =>
        LlmCompletionResult.CreateSuccess(string.Empty) with { ToolCalls = [new LlmToolCall($"call_{Guid.NewGuid():N}", tool, argumentsJson)] };

    private static LlmCompletionResult Says(string text) => LlmCompletionResult.CreateSuccess(text);

    private static LlmCompletionResult Refuses(string errorCode) =>
        LlmCompletionResult.CreateFailure(errorCode) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = errorCode },
        };

    private AskSessionAssistantCommandHandler Handler() => new(new AssistantTurnRunner(
        _directory.Object,
        new SessionAssistantAvailability(_profiles.Object, _catalog.Object),
        _threads.Object,
        _proposals.Object,
        new AssistantChatClientFactory(_gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider()),
        new ExactNameMatcher(),
        _unitOfWork.Object,
        _time,
        NullLogger<AssistantTurnRunner>.Instance));

    private async Task<List<AssistantTurnEvent>> AskAsync(string question = "Marco fa 5 punti", string? language = "it", Guid? user = null)
    {
        var events = new List<AssistantTurnEvent>();
        await foreach (var turnEvent in Handler().Handle(new AskSessionAssistantCommand(Session, user ?? Owner, question, language), Ct))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    // ─── I controlli prima del primo evento ─────────────────────────────────────

    [Fact]
    public async Task SomeoneWhoIsNotTheOwner_IsForbidden_BeforeAnyModelCall()
    {
        var act = () => AskAsync(user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        _gateway.Verify(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACompletedSession_IsAConflict_WithItsCode()
    {
        Access("Completed", accepts: false);

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("session_state_invalid");
    }

    [Fact]
    public async Task ADisabledAssistant_IsAConflict_WithItsCode()
    {
        Publish(SessionAssistantSettings.Disabled);

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("assistant_disabled");
    }

    /// <summary>
    /// La pubblicazione rifiuta questo profilo, ma una migrazione di dati scavalca l'aggregato, e il
    /// catalogo può cambiare dopo: il turno ricontrolla.
    /// </summary>
    [Fact]
    public async Task AModelWithoutToolCalling_IsAConflict_EvenIfTheProfileIsPublished()
    {
        _catalog.Setup(c => c.GetToolCallingModelIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var act = () => AskAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("assistant_model_without_tools");
        _gateway.Verify(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuestion_IsAValidationError(string question)
    {
        var act = () => AskAsync(question);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ATooLongQuestion_IsAValidationError()
    {
        var act = () => AskAsync(new string('a', AssistantTurnRunner.MaxQuestionLength + 1));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ALanguageTagLongerThanATag_IsAValidationError()
    {
        // La lingua arriva dal client e finisce in ogni messaggio salvato: non è un campo libero.
        var act = () => AskAsync(language: new string('x', AssistantTurnRunner.MaxLanguageLength + 1));

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ─── Il turno riuscito ──────────────────────────────────────────────────────

    [Fact]
    public async Task AScoreRequest_SavesTheProposalAndTheMessages_ThenEmitsProposalAnswerDone()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta: +5 a Marco. Confermala dalla card."));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "status", "proposal", "answer", "done");
        events[0].Tool.Should().BeNull();
        events[1].Tool.Should().Be("proponi_punteggio");

        var proposal = _addedProposals.Should().ContainSingle().Subject;
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.CellValue.Should().Be(7);
        proposal.LiveSessionId.Should().Be(Session);
        proposal.ProfileVersionId.Should().Be(VersionId);

        _savedThread.Should().NotBeNull();
        _savedThread!.LiveSessionId.Should().Be(Session);
        _savedThread.GameId.Should().BeNull();
        _savedThread.UserId.Should().Be(Owner);
        _savedThread.AgentType.Should().Be("session-assistant");
        _savedThread.Messages.Select(m => m.Role).Should().Equal("user", "assistant");
        _savedThread.Messages[0].Content.Should().Be("Marco fa 5 punti");
        _savedThread.Messages[1].Id.Should().Be(proposal.MessageId, "la proposta punta al messaggio che l'ha prodotta");
        _savedThread.Messages[1].ProfileVersionId.Should().Be(VersionId);
        _savedThread.Messages[1].Language.Should().Be("it");
        _savedThread.Messages[1].FallbackCode.Should().BeNull("il testo è del modello, e torna al modello nei turni dopo");
        proposal.ChatThreadId.Should().Be(_savedThread.Id);

        events[2].Proposal!.Id.Should().Be(proposal.Id);
        events[2].Proposal!.ResultingValue.Should().Be(12);
        events[3].Text.Should().Be("Ho preparato la proposta: +5 a Marco. Confermala dalla card.");
        events[4].ResponseId.Should().Be(proposal.MessageId);
        events[4].ProfileVersionId.Should().Be(VersionId);
        events[4].ProfileVersionNumber.Should().Be(3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _threads.Verify(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Campo → effetto: ogni campo della sezione del profilo arriva al gateway.</summary>
    [Fact]
    public async Task TheTurn_TakesModelPromptAndLimitsFromTheAssistantSection()
    {
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        var request = _requests.Should().ContainSingle().Subject;
        request.PrimaryModelId.Should().Be("deepseek-flash");
        request.FallbackModelId.Should().BeNull("la sezione non usa la riserva");
        request.Temperature.Should().Be(0.2);
        request.MaxTokens.Should().Be(600);
        request.ProfileVersionId.Should().Be(VersionId);
        request.Source.Should().Be(RequestSource.SessionAssistant);
        request.Tools.Select(t => t.Name).Should().Equal("stato_partita", "proponi_punteggio", "proponi_turno");
        request.Messages[0].Content.Should().Be("Sei l'assistente al tavolo.");
        request.Messages[1].Content.Should().StartWith("Gioco: Azul");
        request.Messages[^1].Content.Should().Be("a chi tocca?");
    }

    [Fact]
    public async Task TheReserveModel_IsUsedOnlyWhenTheSectionSaysSo()
    {
        Publish(AgentProfileTests.Assistant(useReserveModel: true));
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        _requests[0].FallbackModelId.Should().Be(ReserveModel);
    }

    [Theory]
    [InlineData("en-US", "You are the table assistant.", "Game: Azul")]
    [InlineData(null, "You are the table assistant.", "Game: Azul")]
    [InlineData("de", "You are the table assistant.", "Game: Azul")]
    public async Task TheLanguage_PicksThePrompt_AndFallsBackToTheProfileLanguage(string? language, string prompt, string context)
    {
        // «de» non ha un prompt: vale quello della lingua di riserva del profilo («en»), e con lui la lingua del
        // turno (#4397). Prima il contesto statico seguiva la lingua chiesta e restava in italiano, accanto a un
        // prompt inglese.
        Script(Says("ok"));

        await AskAsync("who's next?", language);

        _requests[0].Messages[0].Content.Should().Be(prompt);
        _requests[0].Messages[1].Content.Should().StartWith(context);
    }

    [Fact]
    public async Task TheIterationsOfTheSection_BoundTheLoop_AndAnEmptyAnswerIsSaidExplicitly()
    {
        var oneRound = SessionAssistantSettings.Create(
            true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are the table assistant." },
            maxToolIterations: 1);
        Publish(oneRound);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => _requests.Add(request))
            .ReturnsAsync(() => Calls("stato_partita", "{}"));

        var events = await AskAsync("a chi tocca?");

        _requests.Should().HaveCount(2, "un giro con strumenti, poi la chiamata finale senza");
        _requests[1].Tools.Should().BeEmpty();
        events.Select(e => e.Type).Where(type => type != "status").Should().Equal("answer", "done");
        // #4397: la sezione ha solo il prompt inglese, quindi il turno parla inglese anche se la lingua chiesta
        // è «it»; prima il testo scritto al posto del modello restava in italiano.
        events.Single(e => e.Type == "answer").Text.Should().Be(AssistantTurnTexts.NoAnswer(english: true, hasProposals: false));
        _savedThread!.Messages[1].Language.Should().Be("en");
        _savedThread.Messages[1].FallbackCode.Should().Be(AssistantTurnRunner.NoAnswerCode);
    }

    [Fact]
    public async Task AClaimedActionWithoutAProposal_GetsTheWarning()
    {
        Script(Says("Ho segnato 5 punti a Marco."));

        var events = await AskAsync();

        events.Single(e => e.Type == "answer").Text.Should()
            .Be("Ho segnato 5 punti a Marco." + AssistantTurnTexts.NothingRecorded(english: false));
        _savedThread!.Messages[1].Content.Should().Contain("non è stato registrato nulla");
        _addedProposals.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSecondTurn_ExpiresThePendingProposals_AndTellsTheModelHowTheEarlierOnesEnded()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: Session);
        thread.AddUserMessage("Marco fa 5 punti");
        var earlierAnswer = Guid.NewGuid();
        thread.AddMessage(new DomainChatMessage("Ho preparato la proposta.", DomainChatMessage.AssistantRole, 1, id: earlierAnswer));
        var earlier = AssistantProposal.ForScore(
            Session, thread.Id, earlierAnswer, VersionId, Marco, "Marco", "punti", 2, 5, 7, _time.GetUtcNow().UtcDateTime);
        earlier.Expire(_time.GetUtcNow().UtcDateTime);
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        _proposals.Setup(p => p.ListByThreadAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        _proposals.Verify(p => p.ExpirePendingBySessionAsync(Session, _time.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        var sent = _requests[0].Messages;
        sent.Select(m => m.Role).Should().Equal(
            LlmChatRoles.System, LlmChatRoles.System, LlmChatRoles.User, LlmChatRoles.Assistant, LlmChatRoles.User);
        sent[3].Content.Should().Be("Ho preparato la proposta.\n[+5 punti a Marco, round 2: scaduta senza conferma]");
        _threads.Verify(t => t.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _threads.Verify(t => t.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        thread.Messages.Should().HaveCount(4);
    }

    /// <summary>
    /// #4397: un turno fallito senza proposte non torna al modello, né il testo che il turno ha scritto al
    /// posto suo né la domanda. Il turno riuscito prima di lui sì: la guardia non toglie troppo.
    /// </summary>
    [Fact]
    public async Task AFailedTurnWithoutProposals_DoesNotReachTheModel_QuestionIncluded()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: Session);
        thread.AddUserMessage("chi vince?");
        thread.AddMessage(new DomainChatMessage("Vince Giulia.", DomainChatMessage.AssistantRole, 1));
        thread.AddUserMessage("Marco fa 5 punti");
        var fallback = new DomainChatMessage(
            AssistantTurnTexts.ForError("assistant_unavailable", english: false, hasProposals: false), DomainChatMessage.AssistantRole, 3);
        fallback.MarkAsFallback("assistant_unavailable");
        thread.AddMessage(fallback);
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        var sent = _requests[0].Messages;
        sent.Select(m => m.Role).Should().Equal(
            LlmChatRoles.System, LlmChatRoles.System, LlmChatRoles.User, LlmChatRoles.Assistant, LlmChatRoles.User);
        sent[2].Content.Should().Be("chi vince?");
        sent[3].Content.Should().Be("Vince Giulia.");
        sent[4].Content.Should().Be("a chi tocca?");
        thread.Messages.Should().HaveCount(6, "nella conversazione che vede l'host il turno fallito resta");
    }

    /// <summary>
    /// #4397: se il turno fallito aveva già creato una proposta, il suo esito è un fatto e torna al modello,
    /// con la domanda che l'ha chiesta. Il testo scritto dal turno no.
    /// </summary>
    [Fact]
    public async Task AFailedTurnWithAProposal_KeepsTheQuestionAndTheOutcome_NotTheTextTheTurnWrote()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner, gameId: null, title: null, agentId: null,
            agentType: "session-assistant", liveSessionId: Session);
        thread.AddUserMessage("Marco fa 5 punti");
        var answerId = Guid.NewGuid();
        var fallback = new DomainChatMessage(
            AssistantTurnTexts.ForError("turn_timeout", english: false, hasProposals: true), DomainChatMessage.AssistantRole, 1, id: answerId);
        fallback.MarkAsFallback("turn_timeout");
        thread.AddMessage(fallback);
        var earlier = AssistantProposal.ForScore(
            Session, thread.Id, answerId, VersionId, Marco, "Marco", "punti", 2, 5, 7, _time.GetUtcNow().UtcDateTime);
        earlier.Expire(_time.GetUtcNow().UtcDateTime);
        _threads.Setup(t => t.FindByLiveSessionIdAsync(Session, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        _proposals.Setup(p => p.ListByThreadAsync(thread.Id, It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        Script(Says("Tocca a Giulia."));

        await AskAsync("a chi tocca?");

        var sent = _requests[0].Messages;
        sent.Select(m => m.Role).Should().Equal(
            LlmChatRoles.System, LlmChatRoles.System, LlmChatRoles.User, LlmChatRoles.Assistant, LlmChatRoles.User);
        sent[2].Content.Should().Be("Marco fa 5 punti");
        sent[3].Content.Should().Be("[+5 punti a Marco, round 2: scaduta senza conferma]");
    }

    // ─── Il turno che fallisce a metà (Review Focus 5) ──────────────────────────

    [Fact]
    public async Task ABudgetRefusalMidLoop_KeepsTheProposals_AndEmitsBudgetPaused()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Refuses(LlmBudgetAdmission.DailyBudgetErrorCode));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "status", "proposal", "answer", "error");
        events[^1].Code.Should().Be("budget_paused");
        events[3].Text.Should().Be(AssistantTurnTexts.ForError("budget_paused", english: false, hasProposals: true));
        events[3].Text.Should().Contain("restano valide", "con una card davanti, il testo non manda l'host a segnare a mano");
        _addedProposals.Should().ContainSingle("la proposta creata prima del rifiuto resta confermabile");
        _savedThread!.Messages.Should().HaveCount(2);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AProviderFailure_EmitsAssistantUnavailable()
    {
        Script(Refuses(LlmAnswerErrorCodes.ProviderUnavailable));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "answer", "error");
        events[^1].Code.Should().Be("assistant_unavailable");
        events[1].Text.Should().Be(AssistantTurnTexts.ForError("assistant_unavailable", english: false, hasProposals: false));
        _savedThread!.Messages[1].FallbackCode.Should().Be("assistant_unavailable", "il testo l'ha scritto il turno (#4397)");
    }

    /// <summary>#4397: anche il testo che il turno scrive al posto del modello segue la lingua del prompt.</summary>
    [Fact]
    public async Task WithALanguageWithoutAPrompt_TheTextTheTurnWrites_IsInTheLanguageOfThePrompt()
    {
        Script(Refuses(LlmAnswerErrorCodes.ProviderUnavailable));

        var events = await AskAsync(language: "de");

        _requests[0].Messages[0].Content.Should().Be("You are the table assistant.");
        events.Single(e => e.Type == "answer").Text.Should()
            .Be(AssistantTurnTexts.ForError("assistant_unavailable", english: true, hasProposals: false));
        _savedThread!.Messages[1].Language.Should().Be("en", "è la lingua in cui il turno ha risposto");
    }

    /// <summary>
    /// La scadenza è virtuale (<see cref="TimeProvider"/>). La sezione chiede 120 secondi e il test ne
    /// aspetta al più 30 veri: se la scadenza girasse sull'orologio di sistema, il test fallirebbe per
    /// attesa scaduta invece di passare dopo aver dormito. E prima di far avanzare il tempo finto il
    /// turno deve essere ancora in corso.
    /// </summary>
    [Fact]
    public async Task ATurnThatTakesTooLong_EmitsTurnTimeout_AndStillSavesTheConversation()
    {
        Publish(SessionAssistantSettings.Create(
            true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are the table assistant." },
            turnTimeoutSeconds: 120));
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmChatRequest, CancellationToken>(async (_, token) =>
            {
                called.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Says("mai");
            });

        var asking = AskAsync();
        await called.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        asking.IsCompleted.Should().BeFalse("il gateway non ha risposto e il tempo non è ancora passato");

        _time.Advance(TimeSpan.FromSeconds(121));
        var events = await asking.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        events.Select(e => e.Type).Should().Equal("status", "answer", "error");
        events[^1].Code.Should().Be("turn_timeout");
        _savedThread!.Messages.Should().HaveCount(2);
    }

    /// <summary>
    /// Lo «status» serve mentre il ciclo gira: un turno che trattenesse tutti gli eventi fino alla fine
    /// passerebbe ogni altro test di questa classe.
    /// </summary>
    [Fact]
    public async Task TheStatusOfATool_ReachesTheHost_WhileTheLoopIsStillRunning()
    {
        var release = new TaskCompletionSource<LlmCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmChatRequest, CancellationToken>((_, _) =>
                Interlocked.Increment(ref calls) == 1 ? Task.FromResult(Calls("stato_partita", "{}")) : release.Task);

        await using var events = Handler()
            .Handle(new AskSessionAssistantCommand(Session, Owner, "a chi tocca?", "it"), Ct)
            .GetAsyncEnumerator(Ct);

        (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct)).Should().BeTrue();
        events.Current.Type.Should().Be("status");
        (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct)).Should().BeTrue();
        events.Current.Tool.Should().Be("stato_partita");
        release.Task.IsCompleted.Should().BeFalse("il modello non ha ancora risposto: la seconda chiamata è ferma");

        release.SetResult(Says("Tocca a Giulia."));
        var rest = new List<string>();
        while (await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), Ct))
        {
            rest.Add(events.Current.Type);
        }

        rest.Should().Equal("answer", "done");
    }

    [Fact]
    public async Task AnUnexpectedFailure_EmitsAssistantUnavailable_AndTheStreamStillEnds()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "error");
        events[^1].Code.Should().Be("assistant_unavailable");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Scostamento S6: le card partono solo dopo il salvataggio. Se il salvataggio fallisce, l'host non
    /// deve aver ricevuto una card per una proposta che nel database non esiste.
    /// </summary>
    [Fact]
    public async Task WhenTheSaveFails_NoCardReachesTheHost()
    {
        Script(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta."));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the database is down"));

        var events = await AskAsync();

        _addedProposals.Should().ContainSingle("lo strumento ha girato");
        events.Select(e => e.Type).Should().Equal("status", "status", "error");
        events.Should().NotContain(e => e.Type == "proposal");
        events[^1].Code.Should().Be("assistant_unavailable");
    }

    [Fact]
    public void TheMeter_TellsItsObserverWhichToolWasCalled()
    {
        var seen = new List<string>();
        var meter = new ChatTurnMeter { ToolCalled = seen.Add };

        meter.RecordToolCall("stato_partita");
        meter.RecordToolCall("proponi_turno");

        seen.Should().Equal("stato_partita", "proponi_turno");
        meter.ToolCalls.Should().Equal("stato_partita", "proponi_turno");
    }

    // ─── Ciò che la review dell'intero branch ha chiesto di fissare ─────────────

    /// <summary>
    /// L'host chiude la pagina a metà turno. Lo stream non deve finire prima del turno: il turno usa i
    /// servizi dello scope della richiesta, e toccarli dopo che lo scope è stato rilasciato sarebbe un
    /// ObjectDisposedException sul DbContext. E nulla viene salvato.
    /// </summary>
    [Fact]
    public async Task WhenTheRequestIsCancelled_TheStreamEndsOnlyAfterTheTurnHasStopped_AndNothingIsSaved()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns<LlmChatRequest, CancellationToken>(async (_, token) =>
            {
                entered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    // La chiamata ci mette un momento a fermarsi, come una connessione vera: chi non la
                    // aspetta se ne va prima che sia finita.
                    await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);
                    stopped.TrySetResult();
                }

                return Says("mai");
            });
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var events = Handler()
            .Handle(new AskSessionAssistantCommand(Session, Owner, "a chi tocca?", "it"), request.Token)
            .GetAsyncEnumerator(request.Token);

        (await events.MoveNextAsync()).Should().BeTrue();
        var next = events.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        await request.CancelAsync();

        var act = () => next;
        await act.Should().ThrowAsync<OperationCanceledException>();
        stopped.Task.IsCompleted.Should().BeTrue("lo stream aspetta che il turno si sia fermato");
        await events.DisposeAsync();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Spec §7.1: ChatResponse.Text concatena il testo di tutti i giri. All'host va solo l'ultimo messaggio
    /// dell'assistente, non «Controllo lo stato della partita.» incollato alla risposta.
    /// </summary>
    [Fact]
    public async Task OnlyTheLastAssistantMessage_ReachesTheHost()
    {
        Script(
            LlmCompletionResult.CreateSuccess("Controllo lo stato della partita.") with
            {
                ToolCalls = [new LlmToolCall("call_0", "stato_partita", "{}")],
            },
            Says("Tocca a Giulia."));

        var events = await AskAsync("a chi tocca?");

        events.Single(e => e.Type == "answer").Text.Should().Be("Tocca a Giulia.");
        _savedThread!.Messages[1].Content.Should().Be("Tocca a Giulia.");
    }

    /// <summary>
    /// Un OperationCanceledException che non viene dalla richiesta (la scadenza interna di un servizio) è
    /// un errore come gli altri: se uscisse dal turno, lo stream si chiuderebbe in silenzio.
    /// </summary>
    [Fact]
    public async Task ACancellationThatIsNotTheRequests_IsAnErrorEvent_NotASilentEnd()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("an inner deadline"));

        var events = await AskAsync();

        events.Select(e => e.Type).Should().Equal("status", "error");
        events[^1].Code.Should().Be("assistant_unavailable");
    }

    private sealed class ExactNameMatcher : IAssistantPlayerMatcher
    {
        public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
        {
            var player = players.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            return new AssistantPlayerMatch(player?.Id, player?.Name, IsAmbiguous: false, []);
        }
    }
}
