using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Tests for issue #1703 BE — inline citation markers.
/// Spec-panel decisions D-1703-A (FormatChunkForPrompt [N] index),
/// D-1703-B (AssembleFromContextAsync opt-in flag), D-1703-C (Citation Format section).
///
/// These tests intentionally FAIL/not-compile until Tasks 2-4 add the optional params.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagPromptAssemblyServiceInlineCitationTests
{
    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<ICrossEncoderReranker> _rerankerMock = new();
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly Mock<ITextChunkSearchService> _textSearchMock = new();
    private readonly Mock<IExpansionGameResolver> _expansionResolverMock = new();
    private readonly Mock<IRagEnhancementService> _ragEnhancementMock = new();
    private readonly Mock<IQueryComplexityClassifier> _complexityClassifierMock = new();
    private readonly Mock<IRetrievalRelevanceEvaluator> _relevanceEvaluatorMock = new();
    private readonly Mock<IQueryExpander> _queryExpanderMock = new();
    private readonly Mock<IGraphRetrievalService> _graphRetrievalMock = new();
    private readonly Mock<ILogger<RagPromptAssemblyService>> _loggerMock = new();

    private RagPromptAssemblyService CreateService()
    {
        return new RagPromptAssemblyService(
            _embeddingMock.Object,
            _rerankerMock.Object,
            _llmMock.Object,
            _textSearchMock.Object,
            _expansionResolverMock.Object,
            _ragEnhancementMock.Object,
            _complexityClassifierMock.Object,
            _relevanceEvaluatorMock.Object,
            _queryExpanderMock.Object,
            _graphRetrievalMock.Object,
            _loggerMock.Object);
    }

    private static ChunkCitation BuildCitation(string docId, int page, string snippet, CopyrightTier tier = CopyrightTier.Full)
    {
        return new ChunkCitation(docId, page, 0.85f, snippet, tier) with { FullText = snippet };
    }

    // ── D-1703-A: FormatChunkForPrompt with optional index ──

    [Fact]
    public void FormatChunkForPrompt_WithoutIndex_PreservesLegacyShape()
    {
        var citation = BuildCitation("doc-1", 14, "Sample chunk text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Sample chunk text.");

        result.Should().NotContain("[1]");
        result.Should().StartWith("[Source: Document doc-1");
        result.Should().Contain("Sample chunk text.");
        result.Should().EndWith("---");
    }

    [Fact]
    public void FormatChunkForPrompt_WithIndex1_PrependsBracketedIndex()
    {
        var citation = BuildCitation("doc-1", 14, "Sample chunk text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Sample chunk text.", index: 1);

        result.Should().StartWith("[1] [Source: Document doc-1");
    }

    [Fact]
    public void FormatChunkForPrompt_WithIndex42_PrependsCorrectNumber()
    {
        var citation = BuildCitation("doc-99", 21, "Other text.");

        var result = RagPromptAssemblyService.FormatChunkForPrompt(citation, "Other text.", index: 42);

        result.Should().StartWith("[42] [Source: Document doc-99");
    }

    // ── D-1703-B + D-1703-C: AssembleFromContextAsync with includeInlineCitationInstructions ──

    [Fact]
    public async Task AssembleFromContextAsync_WithoutFlag_DoesNotIncludeCitationFormatSection()
    {
        var service = CreateService();
        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None);

        prompt.SystemPrompt.Should().NotContain("## Citation Format");
        prompt.SystemPrompt.Should().NotContain("prefixed with [N]");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_IncludesCitationFormatSection()
    {
        var service = CreateService();
        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        prompt.SystemPrompt.Should().Contain("## Citation Format");
        prompt.SystemPrompt.Should().Contain("prefixed with [N]");
        prompt.SystemPrompt.Should().Contain("[N,M]");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_PrependsIndexToEachChunkHeader()
    {
        var service = CreateService();
        var chunks = new List<ChunkCitation>
        {
            BuildCitation("doc-1", 14, "first chunk"),
            BuildCitation("doc-2", 21, "second chunk"),
            BuildCitation("doc-3", 7, "third chunk"),
        };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        prompt.SystemPrompt.Should().Contain("[1] [Source: Document doc-1");
        prompt.SystemPrompt.Should().Contain("[2] [Source: Document doc-2");
        prompt.SystemPrompt.Should().Contain("[3] [Source: Document doc-3");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagFalse_DoesNotPrependIndex()
    {
        var service = CreateService();
        var chunks = new List<ChunkCitation>
        {
            BuildCitation("doc-1", 14, "first chunk"),
            BuildCitation("doc-2", 21, "second chunk"),
        };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None);

        // Legacy shape: chunk header starts with "[Source: ...", not "[N] [Source: ...]"
        prompt.SystemPrompt.Should().Contain("[Source: Document doc-1");
        prompt.SystemPrompt.Should().NotContain("[1] [Source: Document doc-1");
        prompt.SystemPrompt.Should().NotContain("[2] [Source: Document doc-2");
    }

    [Fact]
    public async Task AssembleFromContextAsync_WithFlagTrue_PlacesSectionAfterReasoningApproach()
    {
        var service = CreateService();
        var chunks = new List<ChunkCitation> { BuildCitation("doc-1", 14, "text") };

        var prompt = await service.AssembleFromContextAsync(
            agentTypology: "tutor",
            gameTitle: "Gloomhaven",
            userQuestion: "abilities?",
            preRetrievedChunks: chunks,
            chatThread: null,
            userTier: null,
            agentLanguage: "it",
            cancellationToken: CancellationToken.None,
            includeInlineCitationInstructions: true);

        var reasoningIdx = prompt.SystemPrompt.IndexOf("## Reasoning Approach", StringComparison.Ordinal);
        var citationIdx = prompt.SystemPrompt.IndexOf("## Citation Format", StringComparison.Ordinal);
        var ragIdx = prompt.SystemPrompt.IndexOf("## Game Rules and Documentation", StringComparison.Ordinal);

        reasoningIdx.Should().BeGreaterThan(0, "Reasoning Approach section must exist");
        citationIdx.Should().BeGreaterThan(reasoningIdx, "Citation Format must come after Reasoning Approach");
        ragIdx.Should().BeGreaterThan(citationIdx, "Citation Format must come before Game Rules and Documentation");
    }
}
