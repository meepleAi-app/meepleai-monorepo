using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4482: il recupero di una risposta mostrato per intero — il pool che va al reranker e i passaggi
/// finali — per la sonda di recupero. La risposta usa solo i finali, e devono restare quelli di prima.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4482")]
public sealed class AnswerRetrievalTests
{
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000044a2");
    private const string Question = "Posso regalare una risorsa?";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AnswerRetrievalRequest Request => new(Game, Question);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetrieveWithPoolAsync_GivesTheSameFinalsAsRetrieveAsync(bool rerankerEnabled)
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 20, finalTopK: 5, rerankerEnabled: rerankerEnabled);
        var retrieval = AnswerPipelineTestDoubles.Retrieval(
            new FusedSearchStub(FusedSearchStub.Chunks(30)).Handler, ReverseReranker());

        var finals = await retrieval.RetrieveAsync(Request, profile, "it", Ct);
        var traced = await retrieval.RetrieveWithPoolAsync(Request, profile, "it", Ct);

        finals.Should().HaveCount(5);
        traced.FinalResults().Select(r => r.TextContent).Should().Equal(finals.Select(r => r.TextContent));
    }

    [Fact]
    public async Task RetrieveWithPoolAsync_CutsThePoolToTheProfile_InFusionOrder_AndTheFinalsFollowTheReranker()
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 20, finalTopK: 5, rerankerEnabled: true);
        var retrieval = AnswerPipelineTestDoubles.Retrieval(
            new FusedSearchStub(FusedSearchStub.Chunks(30)).Handler, ReverseReranker());

        var traced = await retrieval.RetrieveWithPoolAsync(Request, profile, "it", Ct);

        traced.FusedCount.Should().Be(30);
        traced.Pool.Select(r => r.TextContent).Should().Equal(Enumerable.Range(1, 20).Select(n => $"chunk {n}"));
        traced.Reranking.Should().Be(RerankStatus.Applied);
        // Il reranker di prova rovescia il pool: i finali sono gli ultimi cinque, dal ventesimo.
        traced.Final.Select(p => p.Index).Should().Equal(19, 18, 17, 16, 15);
        traced.Final.Select(p => p.Score).Should().Equal(1.0, 0.9, 0.8, 0.7, 0.6);
        traced.FinalResults().Select(r => r.TextContent).Should().Equal("chunk 20", "chunk 19", "chunk 18", "chunk 17", "chunk 16");
    }

    [Fact]
    public async Task RetrieveWithPoolAsync_WithTheRerankerOff_TakesTheFirstOfThePool_AndSaysSo()
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 20, finalTopK: 5, rerankerEnabled: false);
        var reranker = new Mock<ICrossEncoderReranker>();
        var retrieval = AnswerPipelineTestDoubles.Retrieval(
            new FusedSearchStub(FusedSearchStub.Chunks(30)).Handler, reranker.Object);

        var traced = await retrieval.RetrieveWithPoolAsync(Request, profile, "it", Ct);

        traced.Reranking.Should().Be(RerankStatus.Disabled);
        traced.Final.Should().Equal(Enumerable.Range(0, 5).Select(i => new RerankPick(i, null)));
        reranker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetrieveWithPoolAsync_WithNothingFound_GivesAnEmptyPoolAndNoFinals()
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 20, finalTopK: 5, rerankerEnabled: true);
        var retrieval = AnswerPipelineTestDoubles.Retrieval(new FusedSearchStub([]).Handler, ReverseReranker());

        var traced = await retrieval.RetrieveWithPoolAsync(Request, profile, "it", Ct);

        traced.Pool.Should().BeEmpty();
        traced.Final.Should().BeEmpty();
        traced.FusedCount.Should().Be(0);
        (await retrieval.RetrieveAsync(Request, profile, "it", Ct)).Should().BeEmpty();
    }

    /// <summary>Un reranker che rovescia l'ordine e dà punteggi 1.0, 0.9, 0.8…: l'ordine finale è il suo.</summary>
    internal static ICrossEncoderReranker ReverseReranker()
    {
        var reranker = new Mock<ICrossEncoderReranker>();
        reranker
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
                new RerankResult(
                    chunks.Reverse()
                        .Take(topK ?? chunks.Count)
                        .Select((c, i) => new RerankedChunk(c.Id, c.Content, c.OriginalScore, Math.Round(1.0 - (i * 0.1), 2)))
                        .ToList(),
                    "test-model", 1.0));
        return reranker.Object;
    }
}

/// <summary>#4482: un profilo con i numeri del recupero scelti dal test; il resto è quello del seed.</summary>
internal static class ProbeProfiles
{
    public static AgentProfileContent With(
        int candidatePoolSize = 20,
        int finalTopK = 5,
        decimal minScore = 0.55m,
        decimal vectorWeight = 0.7m,
        decimal keywordWeight = 0.3m,
        bool rerankerEnabled = true) => AgentProfileContent.Create(
        "MeepleAI",
        "Specialized agent for board game rules interpretation and clarification.",
        "a precise board game rules assistant",
        "deepseek-flash",
        "meta-llama/llama-3.3-70b-instruct",
        temperature: 0.3m,
        maxResponseTokens: 1500,
        CitationStyle.PageReferencesInText,
        new Dictionary<string, string> { ["en"] = "You are MeepleAI, a precise board game rules assistant." },
        fallbackLanguage: "en",
        candidatePoolSize: candidatePoolSize,
        finalTopK: finalTopK,
        minScore: minScore,
        vectorWeight: vectorWeight,
        keywordWeight: keywordWeight,
        rerankerEnabled: rerankerEnabled,
        Enum.GetValues<DocumentCategory>(),
        SessionAssistantSettings.Disabled);
}
