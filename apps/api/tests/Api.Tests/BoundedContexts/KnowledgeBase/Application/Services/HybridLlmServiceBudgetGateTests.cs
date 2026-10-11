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
        _gate.Verify(g => g.Complete(It.Is<LlmBudgetAdmission>(a => a.IsAllowed), 0.0003m), Times.Once);
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

        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), 0m), Times.Once);
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

        _gate.Verify(g => g.Complete(It.Is<LlmBudgetAdmission>(a => a.IsAllowed), 0.0003m), Times.Once);
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
}
