using System.Runtime.CompilerServices;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4164 (ADR-095 fetta 1b): il costo di una risposta in streaming finisce nel registro. Prima
/// <c>GenerateCompletionStreamAsync</c> non chiamava mai <see cref="ILlmCostService"/>, e la chat
/// (StreamQa, sessione, cross-game) spendeva senza lasciare righe in <c>llm_cost_logs</c>.
/// </summary>
/// <remarks>
/// «Rosso prima»: i tre test che si aspettano una registrazione sono stati visti fallire contro
/// <c>HybridLlmService</c> di <c>main-dev</c> (<c>3b32c69e0</c>), con «Expected invocation on the
/// mock once, but was 0 times».
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridLlmServiceStreamingCostTests
{
    private readonly Mock<ILlmClient> _client = new();
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();

    private static readonly LlmCost FinalCost = new()
    {
        InputCost = 0.00003m,
        OutputCost = 0.000024m,
        ModelId = "deepseek-chat",
        Provider = "DeepSeek",
        PricingStatus = LlmPricingStatus.Priced
    };

    public HybridLlmServiceStreamingCostTests()
    {
        _client.Setup(c => c.ProviderName).Returns("DeepSeek");
        _client.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);
        _selector
            .Setup(s => s.SelectProviderAsync(
                It.IsAny<LlmUserContext>(), It.IsAny<RagStrategy>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(_client.Object, new LlmRoutingDecision("DeepSeek", "deepseek-chat", "test")));
    }

    private HybridLlmService CreateSut() =>
        new([_client.Object], _selector.Object, _circuitBreakers.Object, _costService.Object, NullLogger<HybridLlmService>.Instance);

    private void ClientStreams(Func<IAsyncEnumerable<StreamChunk>> stream) =>
        _client
            .Setup(c => c.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(stream);

    private static async IAsyncEnumerable<StreamChunk> Chunks(
        IEnumerable<StreamChunk> chunks,
        Exception? throwAtEnd = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }

        if (throwAtEnd is not null)
        {
            throw throwAtEnd;
        }
    }

    private static async Task<List<StreamChunk>> ConsumeAsync(IAsyncEnumerable<StreamChunk> stream)
    {
        var received = new List<StreamChunk>();
        await foreach (var chunk in stream)
        {
            received.Add(chunk);
        }

        return received;
    }

    [Fact]
    public async Task Stream_WithAFinalUsageChunk_LogsItsCost()
    {
        ClientStreams(() => Chunks(
        [
            new StreamChunk("Each player "),
            new StreamChunk("takes four tiles."),
            new StreamChunk(null, new LlmUsage(100, 20, 120), FinalCost, IsFinal: true)
        ]));

        var received = await ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        received.Should().HaveCount(3, "il consumatore riceve gli stessi chunk di prima");
        _costService.Verify(c => c.LogSuccessAsync(
            It.Is<LlmCompletionResult>(r =>
                r.Usage.PromptTokens == 100
                && r.Usage.CompletionTokens == 20
                && r.Cost.TotalCost == 0.000054m
                && r.Cost.ModelId == "deepseek-chat"
                && r.Cost.PricingStatus == LlmPricingStatus.Priced),
            It.IsAny<LlmUserContext>(),
            It.IsAny<long>(),
            RequestSource.RagPipeline,
            It.IsAny<CancellationToken>()), Times.Once);
        _costService.Verify(c => c.LogFailureAsync(
            It.IsAny<string?>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stream_EndingWithoutContentOrUsage_IsLoggedAsAFailure()
    {
        // I client non lanciano: su un errore del provider scrivono nei log e chiudono lo stream.
        ClientStreams(() => Chunks([]));

        await ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        _costService.Verify(c => c.LogFailureAsync(
            It.IsAny<string?>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.RagPipeline, It.IsAny<CancellationToken>()), Times.Once);
        _costService.Verify(c => c.LogSuccessAsync(
            It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stream_ThatThrows_IsLoggedAsAFailure_AndTheExceptionReachesTheConsumer()
    {
        ClientStreams(() => Chunks([new StreamChunk("partial")], new HttpRequestException("connection reset")));

        var consume = () => ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        await consume.Should().ThrowAsync<HttpRequestException>();
        _costService.Verify(c => c.LogFailureAsync(
            "connection reset", It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.RagPipeline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stream_ThatThrowsAfterTheFinalChunk_RecordsOnlyTheSuccess()
    {
        // I token sono stati spesi e il costo è arrivato: l'errore successivo non lo annulla.
        ClientStreams(() => Chunks(
            [new StreamChunk("answer"), new StreamChunk(null, new LlmUsage(100, 20, 120), FinalCost, IsFinal: true)],
            new HttpRequestException("reset after usage")));

        var consume = () => ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        await consume.Should().ThrowAsync<HttpRequestException>();
        _costService.Verify(c => c.LogSuccessAsync(
            It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
        _costService.Verify(c => c.LogFailureAsync(
            It.IsAny<string?>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stream_HttpClientTimeout_IsAFailure_EvenThoughItIsACancellationException()
    {
        // Review di #4182: un timeout di HttpClient arriva come TaskCanceledException senza che
        // il consumatore abbia annullato nulla; è un fallimento del provider.
        ClientStreams(() => Chunks([], new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")));

        var consume = () => ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        await consume.Should().ThrowAsync<TaskCanceledException>();
        _costService.Verify(c => c.LogFailureAsync(
            It.Is<string?>(m => m!.Contains("HttpClient.Timeout")), It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.RagPipeline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stream_CancelledByTheConsumerBeforeAnyContent_IsNotAFailure()
    {
        // I client escono dal loop in silenzio quando il token è annullato (review di #4182):
        // senza il controllo sul token, una disconnessione risulterebbe un fallimento del provider.
        ClientStreams(() => Chunks([]));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await foreach (var _ in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline, cts.Token))
        {
        }

        _costService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Stream_WithContentButNoUsage_RecordsNothing()
    {
        // Ollama può chiudere uno stream riuscito senza conteggi: non è un fallimento, ma non c'è
        // un costo da registrare.
        ClientStreams(() => Chunks([new StreamChunk("an answer"), new StreamChunk(null, IsFinal: true)]));

        await ConsumeAsync(CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline));

        _costService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Stream_StoppedByTheConsumerBeforeTheEnd_RecordsNothing()
    {
        ClientStreams(() => Chunks(
        [
            new StreamChunk("first"),
            new StreamChunk("second"),
            new StreamChunk(null, new LlmUsage(100, 20, 120), FinalCost, IsFinal: true)
        ]));

        await foreach (var _ in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline))
        {
            break;
        }

        _costService.VerifyNoOtherCalls();
    }
}
