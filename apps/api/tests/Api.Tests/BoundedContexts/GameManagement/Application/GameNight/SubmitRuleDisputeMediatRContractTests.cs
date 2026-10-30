using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4751 — the Arbitro through the REAL MediatR container: the command handler of GameManagement, the
/// <c>internal</c> <c>AskGroundedSessionQuery</c> of KnowledgeBase (the cross-BC contract), the real
/// <c>GroundedAnswerService</c>, copyright leak guard and tier resolver. Only the retrieval, the LLM and the
/// data projection are doubles. The unit tests mock the mediator, so they cannot see a handler that is not
/// registered, a tier that is not the one asked for, or a leak guard whose fallback message ends up as a verdict.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4751")]
public sealed class SubmitRuleDisputeMediatRContractTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    private const string Dispute = "Il ladro si sposta subito?";

    /// <summary>A passage of a Protected rulebook, word for word.</summary>
    private const string ChunkA =
        "Quando un giocatore ottiene un sette sposta immediatamente il ladro su un altro esagono e ruba una carta risorsa a un avversario che possiede una colonia adiacente.";

    private sealed record Harness(
        IMediator Mediator,
        LiveGameSession Session,
        Mock<IPublisher> Publisher,
        Mock<ILlmService> Llm,
        Mock<IRagPromptAssemblyService> Retrieval,
        Mock<ITierEnforcementService> Quota,
        Mock<IAnswerPreconditions> Gate);

    private static Harness Build(
        string llmAnswer,
        IReadOnlyList<ChunkCitation> chunks,
        Dictionary<string, PdfCopyrightInfo> pdfInfos,
        bool callerOwnsTheGame = false,
        bool retrievalFailed = false,
        bool gateDenies = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Retrieval: a double, because it is the part that needs a vector store
        var retrieval = new Mock<IRagPromptAssemblyService>();
        retrieval
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()))
            .ReturnsAsync(new AssembledPrompt("SYS-BASE", "USER", chunks.ToList(), 10, retrievalFailed));
        services.AddSingleton(retrieval.Object);

        // Data projection: a double. When the caller owns the game, that is true for the caller and false for
        // anyone else — in particular for Guid.Empty, the tier the table is written at.
        var projection = new Mock<ICopyrightDataProjection>();
        projection
            .Setup(p => p.GetPdfCopyrightInfoAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> ids, CancellationToken _) =>
                (IReadOnlyDictionary<string, PdfCopyrightInfo>)pdfInfos
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value));
        projection
            .Setup(p => p.CheckOwnershipAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid user, IReadOnlyList<Guid> games, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, bool>)(callerOwnsTheGame && user == UserId
                    ? games.ToDictionary(g => g, _ => true)
                    : new Dictionary<Guid, bool>()));
        services.AddSingleton(projection.Object);

        // The real KnowledgeBase pipeline after the retrieval
        services.AddSingleton<ICopyrightTierResolver, CopyrightTierResolver>();
        services.AddSingleton<ICopyrightLeakGuard, NgramCopyrightLeakGuard>();
        services.AddSingleton<ICopyrightFallbackMessageProvider, DefaultCopyrightFallbackMessageProvider>();
        services.AddSingleton(Options.Create(new CopyrightLeakGuardOptions()));
        services.AddSingleton<IGroundedAnswerService, GroundedAnswerService>();

        var llm = new Mock<ILlmService>();
        llm
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmAnswer));
        services.AddSingleton(llm.Object);

        var gate = new Mock<IAnswerPreconditions>();
        gate
            .Setup(g => g.EnsureCanAskAboutGameAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(gateDenies
                ? Task.FromException(new RagOwnershipRequiredException(GameId))
                : Task.CompletedTask);
        services.AddSingleton(gate.Object);

        var session = LiveGameSession.Create(SessionId, UserId, "Catan", TimeProvider.System, gameId: GameId);
        session.AddPlayer(UserId, "Alice", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(r => r.GetByIdAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(repository.Object);

        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = SessionId,
            SessionCode = session.SessionCode,
            GameName = "Catan",
            CreatedByUserId = UserId,
            Status = (int)LiveSessionStatus.InProgress,
            CurrentTurnIndex = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AgentMode = (int)AgentSessionMode.None,
            ScoringConfigJson = "{}",
            Xmin = 1u,
        });
        db.SaveChanges();
        services.AddSingleton<MeepleAiDbContext>(db);

        var quota = new Mock<ITierEnforcementService>();
        quota
            .Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        quota
            .Setup(t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(quota.Object);

        var publisher = new Mock<IPublisher>();
        services.AddSingleton(publisher.Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        return new Harness(provider.GetRequiredService<IMediator>(), session, publisher, llm, retrieval, quota, gate);
    }

    private static ChunkCitation Chunk(string document, int page, float score = 0.9f)
        => new(document, page, score, ChunkA[..117] + "...") { FullText = ChunkA, ChunkIndex = 1 };

    private static PdfCopyrightInfo Info(DocumentCategory category, Guid? uploadedBy = null)
        => new("doc", LicenseType.Copyrighted, category, uploadedBy ?? Guid.NewGuid(), GameId, null, false);

    private static Task<RuleDisputeResponse> Submit(Harness h)
        => h.Mediator.Send(new SubmitRuleDisputeCommand(SessionId, UserId, Dispute, "Alice", CallerRole: null));

    /// <summary>The model answers twice: first with <paramref name="first"/>, then with <paramref name="second"/>.</summary>
    private static void TheModelAnswersTwice(Harness h, LlmCompletionResult first, LlmCompletionResult second)
        => h.Llm
            .SetupSequence(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(first)
            .ReturnsAsync(second);

    private static void NothingWasSavedChargedOrPublished(Harness h)
    {
        h.Session.Disputes.Should().BeEmpty();
        h.Quota.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        h.Publisher.Verify(
            p => p.Publish(It.IsAny<DisputeResolvedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── The contract, end to end ─────────────────────────────────────────────

    [Fact]
    public async Task AProtectedParaphrase_GoesThroughTheRealPipeline_AndIsCitedByPageOnly()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: [ref:docA:12] Quando esce il sette il ladro si muove subito.\nNOTA: Nessuna.",
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) });

        var result = await Submit(h);

        result.Verdict.Should().Be("Quando esce il sette il ladro si muove subito.");
        result.RuleReferences.Should().Equal("Fonte, p. 12");
        h.Session.Disputes.Should().ContainSingle();
    }

    [Fact]
    public async Task TheDisputeAloneIsTheRetrievalQuery_AndThePersonaAndFormatGoToGeneration()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: Si muove subito.",
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) });
        string? generationPrompt = null;
        h.Llm
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((system, _, _, _) => generationPrompt = system)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("COPERTURA: SI\nVERDETTO: Si muove subito."));

        await Submit(h);

        h.Retrieval.Verify(
            r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                Dispute, GameId, It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), "it", It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()),
            Times.Once);
        generationPrompt.Should().StartWith("SYS-BASE");
        generationPrompt.Should().Contain("COPERTURA:").And.Contain("VERDETTO:").And.Contain("ARBITRO");
    }

    [Fact]
    public async Task AFullCategoryDocument_IsQuotedInTheReference()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: Si sposta subito.\nNOTA: Nessuna.",
            new[] { Chunk("docF", 3) },
            new() { ["docF"] = Info(DocumentCategory.Reference) });

        var result = await Submit(h);

        var reference = result.RuleReferences.Should().ContainSingle().Which;
        reference.Should().StartWith("Fonte, p. 3: «Quando un giocatore ottiene un sette");
    }

    [Fact]
    public async Task WhenTheCallerUploadedAndOwnsTheFile_TheTableStillGetsTheProtectedTier()
    {
        // The caller would see this file as Full (rule 3: uploader AND owner). The verdict is written at
        // Guid.Empty — so no preview in the references, and a verbatim run is caught by the leak guard.
        var h = Build(
            "COPERTURA: SI\nVERDETTO: Si sposta subito.\nNOTA: Nessuna.",
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook, uploadedBy: UserId) },
            callerOwnsTheGame: true);

        var result = await Submit(h);

        result.RuleReferences.Should().Equal("Fonte, p. 12");
    }

    // ─── The leak guard's fallback is not a verdict, and not a dead end ────────

    [Theory]
    [InlineData("COPERTURA: SI\nVERDETTO: " + ChunkA + "\nNOTA: Nessuna.")]
    [InlineData("COPERTURA: SI\nVERDETTO: Si sposta subito.\nNOTA: " + ChunkA)]
    public async Task AVerbatimRunOfAProtectedPassage_GivesTheVerdictOnceWithoutTheRulebook_NotTheFallbackMessage(
        string answerThatRepeatsThePassage)
    {
        // The real leak guard replaces the WHOLE answer when the run is in the verdict, or in the note alone. The
        // fallback message used to be saved as the verdict and charged; failing instead would leave a player who
        // pastes a passage into the dispute in an error on every attempt. The second answer is the direct one.
        var h = Build(
            answerThatRepeatsThePassage,
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) });
        TheModelAnswersTwice(
            h,
            LlmCompletionResult.CreateSuccess(answerThatRepeatsThePassage),
            LlmCompletionResult.CreateSuccess("VERDETTO: Si sposta subito.\nNOTA: Nessuna."));

        var result = await Submit(h);

        result.Verdict.Should().StartWith(ArbiterVerdictFormat.CopyrightWithheldNotice);
        result.Verdict.Should().Contain("Si sposta subito.");
        result.Verdict.Should().NotContain("sposta immediatamente il ladro su un altro esagono");
        result.RuleReferences.Should().BeEmpty();
        h.Session.Disputes.Should().ContainSingle();
        h.Retrieval.Verify(
            r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()),
            Times.Once);
        h.Quota.Verify(
            t => t.RecordUsageAsync(UserId, TierAction.SessionAgentQuery, It.IsAny<CancellationToken>()), Times.Once);
        h.Publisher.Verify(
            p => p.Publish(It.IsAny<DisputeResolvedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenTheSecondAttemptFailsToo_AfterAVerbatimRun_NothingIsSavedChargedOrPublished()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: " + ChunkA,
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) });
        TheModelAnswersTwice(
            h,
            LlmCompletionResult.CreateSuccess("COPERTURA: SI\nVERDETTO: " + ChunkA),
            LlmCompletionResult.CreateFailure("LLM unavailable"));

        var act = () => Submit(h);
        var ex = (await act.Should().ThrowAsync<ExternalServiceException>()).Which;

        ex.StatusCode.Should().Be(503);
        ex.ErrorCode.Should().Be("arbitro_nessun_verdetto");
        NothingWasSavedChargedOrPublished(h);
    }

    // ─── What the verdict rests on ────────────────────────────────────────────

    [Fact]
    public async Task WhenTheModelSaysThePassagesDoNotCoverTheCase_ItIsDeclaredAndNothingIsCited()
    {
        var h = Build(
            "COPERTURA: NO\nVERDETTO: I passaggi non coprono il caso; l'interpretazione più ragionevole è che si sposti subito.\nNOTA: Interpretazione.",
            new[] { Chunk("docA", 7) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) });

        var result = await Submit(h);

        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NotCoveredNotice);
    }

    [Fact]
    public async Task WhenNothingWasRetrieved_TheVerdictSaysSo()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: Si sposta subito.",
            Array.Empty<ChunkCitation>(),
            new());

        var result = await Submit(h);

        result.RuleReferences.Should().BeEmpty();
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.NoPassagesNotice);
    }

    [Fact]
    public async Task WhenTheRetrievalFailed_TheVerdictSaysTheRulebookCouldNotBeConsulted()
    {
        var h = Build(
            "COPERTURA: SI\nVERDETTO: Si sposta subito.",
            Array.Empty<ChunkCitation>(),
            new(),
            retrievalFailed: true);

        var result = await Submit(h);

        result.Verdict.Should().StartWith(ArbiterVerdictFormat.RetrievalUnavailableNotice);
    }

    [Fact]
    public async Task WhenTheGateDenies_TheRetrievalIsNeverStarted()
    {
        var h = Build(
            "VERDETTO: Si sposta subito.",
            new[] { Chunk("docA", 12) },
            new() { ["docA"] = Info(DocumentCategory.Rulebook) },
            gateDenies: true);

        var result = await Submit(h);

        h.Retrieval.Verify(
            r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()),
            Times.Never);
        result.Verdict.Should().StartWith(ArbiterVerdictFormat.OwnershipNotDeclaredNotice);
        result.RuleReferences.Should().BeEmpty();
    }
}
