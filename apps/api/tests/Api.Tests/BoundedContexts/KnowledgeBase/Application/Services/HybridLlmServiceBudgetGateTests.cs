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
/// #4166 (ADR-095 D4 e D6): il gateway chiede al gate prima di ogni chiamata, e una chiamata
/// rifiutata non raggiunge il provider.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4166")]
public sealed class HybridLlmServiceBudgetGateTests
{
    private readonly Mock<ILlmClient> _deepSeek = new();
    private readonly Mock<ILlmClient> _openRouter = new();
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly Mock<IAgentReserveModelProvider> _reserve = new();

    public HybridLlmServiceBudgetGateTests()
    {
        foreach (var (client, provider, model) in new[] { (_deepSeek, "DeepSeek", "deepseek-flash"), (_openRouter, "OpenRouter", "meta-llama/llama-3.3-70b-instruct") })
        {
            client.Setup(c => c.ProviderName).Returns(provider);
            client.Setup(c => c.SupportsModel(model)).Returns(true);
            client
                .Setup(c => c.GenerateCompletionAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                    $"answer from {provider}",
                    new LlmUsage(100, 20, 120),
                    new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider }));
            client
                .Setup(c => c.GenerateCompletionStreamAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(() => AnswerStream(model, provider));
        }

        _selector
            .Setup(s => s.SelectProviderAsync(
                It.IsAny<LlmUserContext>(), It.IsAny<RagStrategy>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(_deepSeek.Object, new LlmRoutingDecision("DeepSeek", "deepseek-flash", "test")));
        _selector
            .Setup(s => s.GetNextFallbackAsync("DeepSeek", It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(_openRouter.Object,
                new LlmRoutingDecision("OpenRouter", "meta-llama/llama-3.3-70b-instruct", "fallback")));
        _selector
            .Setup(s => s.GetNextFallbackAsync("OpenRouter", It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderSelectionResult.NoProvider("none left"));

        AllowEverything();
    }

    private HybridLlmService CreateSut() =>
        new([_deepSeek.Object, _openRouter.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object);

    private static LlmBudgetAdmission Admission(LlmBudgetRejection rejection, string? message = null) => new()
    {
        ReservationId = rejection == LlmBudgetRejection.None ? Guid.NewGuid() : Guid.Empty,
        Rejection = rejection,
        BudgetClass = LlmBudgetClass.Answer,
        EstimatedCostUsd = 0.004m,
        Message = message,
    };

    private void AllowEverything() =>
        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Admission(LlmBudgetRejection.None));

    private void Refuse(string model, LlmBudgetRejection rejection) =>
        _gate.Setup(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.ModelId == model), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Admission(rejection, $"refused: {rejection}"));

    private static async IAsyncEnumerable<StreamChunk> AnswerStream(
        string model, string provider, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return new StreamChunk("answer");
        yield return new StreamChunk(
            null,
            new LlmUsage(100, 20, 120),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider },
            IsFinal: true);
    }

    private static void ProviderNeverCalled(Mock<ILlmClient> client)
    {
        client.Verify(c => c.GenerateCompletionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.GenerateCompletionStreamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OverTheCap_TheCallDoesNotReachTheProvider_NorItsFallback()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.DailyCapReached);

        var result = await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DailyCapReached");
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        ProviderNeverCalled(_deepSeek);
        ProviderNeverCalled(_openRouter);
    }

    [Fact]
    public async Task AModelOutsideTheCatalog_FallsBackToTheNextProvider()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.ModelNotInCatalog);

        var result = await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("answer from OpenRouter");
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task TheGateSeesTheCall_AndIsClosedWithTheActualCost()
    {
        await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.RagPipeline);

        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.Source == RequestSource.RagPipeline && r.ModelId == "deepseek-flash"
                && r.Provider == "DeepSeek" && r.PromptCharacters == "system".Length + "question".Length && r.MaxOutputTokens > 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _gate.Verify(g => g.Complete(It.Is<LlmBudgetAdmission>(a => a.IsAllowed), 0.0003m, true), Times.Once);
    }

    [Fact]
    public async Task AFailedCall_IsClosedAtZero()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        _selector
            .Setup(s => s.GetNextFallbackAsync("DeepSeek", It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderSelectionResult.NoProvider("none left"));

        await CreateSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), 0m, true), Times.Once);
    }

    [Fact]
    public async Task Streaming_OverTheCap_YieldsOnlyTheReason()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.DailyCapReached);

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.Manual))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle();
        chunks[0].ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        chunks[0].ErrorMessage.Should().Contain("DailyCapReached");
        chunks[0].Content.Should().BeNull();
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task Streaming_IsClosedWithTheCostOfTheFinalChunk()
    {
        await foreach (var _ in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.Manual))
        {
        }

        _gate.Verify(g => g.Complete(It.Is<LlmBudgetAdmission>(a => a.IsAllowed), 0.0003m, true), Times.Once);
    }

    /// <summary>
    /// Review della 3a: il consumatore che smette di leggere (l'utente chiude la chat) lascia il
    /// costo senza chunk finale. Il provider lo fattura: il gate lo conta con la stima, fuori registro.
    /// </summary>
    [Fact]
    public async Task Streaming_StoppedByTheConsumerAfterContent_CountsTheEstimate()
    {
        await foreach (var _ in CreateSut().GenerateCompletionStreamAsync("system", "question", RequestSource.Manual))
        {
            break;
        }

        _gate.Verify(g => g.Complete(It.Is<LlmBudgetAdmission>(a => a.IsAllowed), 0.004m, false), Times.Once);
    }

    [Fact]
    public async Task Multimodal_OverTheCap_DoesNotReachTheVisionProvider()
    {
        _deepSeek.Setup(c => c.SupportsVision).Returns(true);
        Refuse("deepseek-flash", LlmBudgetRejection.DailyCapReached);
        var messages = new List<LlmMessage>
        {
            new("user", [new TextContentPart("What is on this card?"), new ImageContentPart("aGVsbG8=", "image/png")]),
        };

        var result = await CreateSut().GenerateMultimodalCompletionAsync(messages, RequestSource.Manual);

        result.Success.Should().BeFalse();
        _gate.Verify(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.ImageCount == 1), It.IsAny<CancellationToken>()), Times.Once);
        _deepSeek.Verify(c => c.GenerateCompletionAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<LlmMessage>>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExplicitModel_OverTheCap_DoesNotReachTheProvider()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.MonthlyCapReached);

        var result = await CreateSut().GenerateCompletionWithModelAsync("deepseek-flash", "system", "question", RequestSource.ChunkTranslation);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.MonthlyBudgetErrorCode);
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task ExplicitModel_OutsideTheCatalog_IsRefused()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.ModelNotInCatalog);

        var result = await CreateSut().GenerateCompletionWithModelAsync("deepseek-flash", "system", "question", RequestSource.AdminOperation);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.ModelErrorCode);
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task ModelWithFallback_OutsideTheCatalog_TriesTheNextProvider()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.ModelNotInCatalog);

        var result = await CreateSut().GenerateCompletionWithModelFallbackAsync("deepseek-flash", "system", "question", RequestSource.AdminOperation);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("answer from OpenRouter");
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task ExplicitModelStream_PassesTheAgentParameters_AndIsGated()
    {
        await foreach (var _ in CreateSut().GenerateCompletionStreamWithModelAsync(
            "deepseek-flash", "system", "question", RequestSource.AdminOperation, temperature: 0.9, maxTokens: 321))
        {
        }

        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.ModelId == "deepseek-flash" && r.MaxOutputTokens == 321 && r.Source == RequestSource.AdminOperation),
            It.IsAny<CancellationToken>()), Times.Once);
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(
            "deepseek-flash", "system", "question", 0.9, 321, It.IsAny<CancellationToken>()), Times.Once);
        _costService.Verify(c => c.LogSuccessAsync(
            It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(), RequestSource.AdminOperation, It.IsAny<CancellationToken>()),
            Times.Once, "il costo del playground lo registra ora il gateway");
    }

    [Fact]
    public async Task ExplicitModelStream_OverTheCap_YieldsOnlyTheReason()
    {
        Refuse("deepseek-flash", LlmBudgetRejection.DailyQuotaReached);

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in CreateSut().GenerateCompletionStreamWithModelAsync(
            "deepseek-flash", "system", "question", RequestSource.AdminOperation))
        {
            chunks.Add(chunk);
        }

        chunks.Should().ContainSingle().Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        ProviderNeverCalled(_deepSeek);
    }

    private const string ReserveModel = "meta-llama/llama-3.3-70b-instruct";

    private HybridLlmService CreateDegradableSut() =>
        new([_deepSeek.Object, _openRouter.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object, reserveModels: _reserve.Object);

    private void Degraded(bool degraded = true, string? reserve = ReserveModel, bool reserveIsCheaper = true)
    {
        _gate.Setup(g => g.IsDegradedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(degraded);
        _gate.Setup(g => g.IsCheaperReserveAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reserveIsCheaper);
        _reserve.Setup(r => r.GetReserveModelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reserve);
    }

    private void AdmittedWith(string model, string provider) =>
        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.ModelId == model && r.Provider == provider), It.IsAny<CancellationToken>()), Times.Once);

    /// <summary>3b (D6): fra la soglia di degrado e il tetto la risposta passa al modello di riserva del profilo.</summary>
    [Fact]
    public async Task OverTheDegradeThreshold_AnAnswerUsesTheReserveModelOfTheProfile()
    {
        Degraded();

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from OpenRouter");
        _openRouter.Verify(c => c.GenerateCompletionAsync(
            ReserveModel, "system", "question", It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        ProviderNeverCalled(_deepSeek);
        AdmittedWith(ReserveModel, "OpenRouter");
    }

    [Fact]
    public async Task OverTheDegradeThreshold_AStreamedAnswerUsesTheReserveModel()
    {
        Degraded();

        await foreach (var _ in CreateDegradableSut().GenerateCompletionStreamAsync("system", "question", RequestSource.RagPipeline))
        {
        }

        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(
            ReserveModel, "system", "question", It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        ProviderNeverCalled(_deepSeek);
        AdmittedWith(ReserveModel, "OpenRouter");
    }

    [Fact]
    public async Task UnderTheDegradeThreshold_TheMainModelAnswers()
    {
        Degraded(degraded: false);

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
        _reserve.Verify(r => r.GetReserveModelAsync(It.IsAny<CancellationToken>()), Times.Never, "senza degrado il profilo non si legge");
    }

    [Fact]
    public async Task ABackgroundCall_IsNotMovedToTheReserve()
    {
        Degraded();

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.ChunkTranslation);

        result.Response.Should().Be("answer from DeepSeek", "il background lo ferma il gate, non lo devia il gateway");
        _gate.Verify(g => g.IsDegradedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AReserveNoProviderServes_KeepsTheMainModel()
    {
        Degraded(reserve: "some-vendor/unknown-model");

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
    }

    [Fact]
    public async Task AReserveWhoseCircuitIsOpen_KeepsTheMainModel()
    {
        Degraded();
        _circuitBreakers.Setup(c => c.GetState("OpenRouter")).Returns(CircuitState.Open);

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
    }

    [Fact]
    public async Task AProfileWithoutAReserve_KeepsTheMainModel()
    {
        Degraded(reserve: null);

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
    }

    /// <summary>
    /// Review della 3b: il gateway chiede al gate se la riserva è a catalogo e costa meno del modello
    /// instradato, e se no resta sul principale.
    /// </summary>
    [Fact]
    public async Task AReserveThatIsNotCheaperOrNotInTheCatalog_KeepsTheMainModel()
    {
        Degraded(reserveIsCheaper: false);

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
        _gate.Verify(g => g.IsCheaperReserveAsync("deepseek-flash", "DeepSeek", ReserveModel, "OpenRouter", It.IsAny<CancellationToken>()),
            Times.Once);
    }

#pragma warning disable CS1998 // uno stream che si chiude senza chunk, come i client quando il provider fallisce
    private static async IAsyncEnumerable<StreamChunk> EmptyStream()
    {
        yield break;
    }
#pragma warning restore CS1998

    private void ReserveStreamFails() =>
        _openRouter
            .Setup(c => c.GenerateCompletionStreamAsync(
                ReserveModel, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => EmptyStream());

    private async Task<List<StreamChunk>> StreamAsync(RequestSource source = RequestSource.Manual)
    {
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in CreateDegradableSut().GenerateCompletionStreamAsync("system", "question", source))
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Trovato nella verifica a runtime della 3b: con la chiave di OpenRouter rifiutata (401), la
    /// riserva chiudeva lo stream senza contenuto e l'utente riceveva una risposta vuota, dove senza
    /// degrado avrebbe risposto il modello instradato.
    /// </summary>
    [Fact]
    public async Task Streaming_AReserveThatProducesNothing_FallsBackToTheRoutedModel()
    {
        Degraded();
        ReserveStreamFails();

        var chunks = await StreamAsync();

        chunks.Should().Contain(c => c.Content == "answer");
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(
            "deepseek-flash", "system", "question", It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        AdmittedWith("deepseek-flash", "DeepSeek");
    }

    [Fact]
    public async Task Streaming_AReserveRefusedByTheBudget_DoesNotFallBack()
    {
        Degraded();
        Refuse(ReserveModel, LlmBudgetRejection.DailyCapReached);

        var chunks = await StreamAsync();

        chunks.Should().ContainSingle().Which.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode,
            "oltre il tetto il modello instradato verrebbe rifiutato allo stesso modo");
        ProviderNeverCalled(_deepSeek);
    }

    [Fact]
    public async Task Streaming_WithoutDegrade_AnEmptyStreamIsNotRetried()
    {
        Degraded(degraded: false);
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => EmptyStream());

        var chunks = await StreamAsync();

        chunks.Should().BeEmpty();
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedReserve_FallsBackToTheRoutedModel_BeforeTheFallbackChain()
    {
        Degraded();
        _openRouter
            .Setup(c => c.GenerateCompletionAsync(
                ReserveModel, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("OpenRouter API error: 401 (Unauthorized)"));

        var result = await CreateDegradableSut().GenerateCompletionAsync("system", "question", RequestSource.Manual);

        result.Response.Should().Be("answer from DeepSeek");
        _selector.Verify(s => s.GetNextFallbackAsync("OpenRouter", It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()), Times.Never,
            "la scelta dell'instradamento viene prima della catena di fallback");
    }
}
