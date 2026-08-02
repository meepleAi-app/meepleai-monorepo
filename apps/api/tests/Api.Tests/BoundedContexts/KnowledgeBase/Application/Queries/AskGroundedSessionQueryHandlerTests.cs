using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #3390 Slice 2: <see cref="AskGroundedSessionQueryHandler"/> produces a grounded, non-streaming
/// answer for the in-session image/text path. Grounding is derived from the citation count
/// (Grounded iff citations > 0), mirroring the SSE text path's #3388 invariant.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3390")]
public sealed class AskGroundedSessionQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithCitations_ReturnsGroundedAnswerWithMappedCitations()
    {
        // Arrange — retrieval yields one Full-tier citation.
        var citations = new List<ChunkCitation>
        {
            new ChunkCitation(
                DocumentId: "doc-1",
                PageNumber: 4,
                RelevanceScore: 0.87f,
                SnippetPreview: "Victory points are scored at game end.",
                CopyrightTier: CopyrightTier.Full,
                IsPublic: true),
        };
        var handler = BuildHandler(citations, llmAnswer: "You score victory points at game end. [Page 4]");

        // Act
        var result = await handler.Handle(
            new AskGroundedSessionQuery(Guid.NewGuid(), "How do I score points?", GameStateContext: null, UserId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.GroundingStatus.Should().Be(GroundingStatus.Grounded, "citations were produced");
        result.Citations.Should().ContainSingle();
        result.Citations[0].DocumentId.Should().Be("doc-1");
        result.Citations[0].PageNumber.Should().Be(4);
        result.Citations[0].SnippetPreview.Should().Be("Victory points are scored at game end.", "Full-tier keeps the preview");
        result.Answer.Should().Contain("[Page 4]");
        result.Confidence.Should().NotBeNull("ComputeConfidence returns a value when citations exist");
    }

    [Fact]
    public async Task Handle_NoCitations_ReturnsUngrounded()
    {
        // Arrange — retrieval yields nothing (query not covered by the rulebook).
        var handler = BuildHandler(new List<ChunkCitation>(), llmAnswer: "I couldn't find that in the rulebook.");

        // Act
        var result = await handler.Handle(
            new AskGroundedSessionQuery(Guid.NewGuid(), "What is the airspeed of a swallow?", GameStateContext: null, UserId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.GroundingStatus.Should().Be(GroundingStatus.Ungrounded, "zero citations -> ungrounded (never fabricate grounded)");
        result.Citations.Should().BeEmpty();
        result.Confidence.Should().BeNull("ComputeConfidence returns null with no citations");
    }

    [Fact]
    public async Task Handle_GenerationFails_ThrowsSoCallerCanDegrade()
    {
        // Arrange — retrieval works but the LLM call fails.
        var handler = BuildHandler(
            new List<ChunkCitation>(),
            llmAnswer: null,
            llmSuccess: false);

        // Act
        var act = () => handler.Handle(
            new AskGroundedSessionQuery(Guid.NewGuid(), "Question", GameStateContext: null, UserId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert — SessionTracking catches this to fall back to the multimodal-only path.
        await act.Should().ThrowAsync<GroundedSessionGenerationException>();
    }

    [Fact]
    public async Task Handle_SuccessButEmptyResponse_Throws()
    {
        // Arrange — provider returns Success=true but a blank body. Must be treated as a failure
        // so the caller degrades, not shipped as a blank "grounded" answer (would 500 downstream).
        var handler = BuildHandler(new List<ChunkCitation>(), llmAnswer: "   ", llmSuccess: true);

        // Act
        var act = () => handler.Handle(
            new AskGroundedSessionQuery(Guid.NewGuid(), "Question", GameStateContext: null, UserId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GroundedSessionGenerationException>();
    }

    private static AskGroundedSessionQueryHandler BuildHandler(
        IReadOnlyList<ChunkCitation> citations,
        string? llmAnswer,
        bool llmSuccess = true)
    {
        var assembled = new AssembledPrompt(
            SystemPrompt: "You are a helpful board game tutor.",
            UserPrompt: "Context...\n\nQuestion",
            Citations: citations.ToList(),
            EstimatedTokens: 100);

        var ragPrompt = new Mock<IRagPromptAssemblyService>();
        ragPrompt
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()))
            .ReturnsAsync(assembled);

        var tierResolver = new Mock<ICopyrightTierResolver>();
        tierResolver
            .Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(citations);

        var llm = new Mock<ILlmService>();
        llm
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmSuccess
                ? LlmCompletionResult.CreateSuccess(llmAnswer ?? "answer")
                : LlmCompletionResult.CreateFailure("llm down"));

        var leakGuard = new Mock<ICopyrightLeakGuard>();
        leakGuard
            .Setup(g => g.ScanAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CopyrightLeakResult(HasLeak: false, Matches: Array.Empty<LeakMatch>()));

        var fallbackProvider = new Mock<ICopyrightFallbackMessageProvider>();
        fallbackProvider.Setup(f => f.GetMessage(It.IsAny<string>())).Returns("[copyright fallback]");

        // #3490: the handler delegates finalize to the shared GroundedAnswerService — use the REAL
        // service (with mocked leak guard / fallback) so the finalize logic stays exercised end-to-end.
        var groundedAnswerService = new GroundedAnswerService(
            leakGuard.Object,
            fallbackProvider.Object,
            Options.Create(new CopyrightLeakGuardOptions()),
            NullLogger<GroundedAnswerService>.Instance);

        return new AskGroundedSessionQueryHandler(
            ragPrompt.Object,
            tierResolver.Object,
            llm.Object,
            groundedAnswerService);
    }
}
