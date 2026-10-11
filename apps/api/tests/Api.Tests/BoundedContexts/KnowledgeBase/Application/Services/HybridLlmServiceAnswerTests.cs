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

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4168 (ADR-095 D3, passo 6): la risposta della pipeline passa dal gateway con modello, riserva,
/// temperatura e lunghezza decisi dal chiamante. Le garanzie, uguali in streaming e in risposta
/// unica: fallback sulla riserva solo prima del primo token, circuit breaker aggiornato a ogni
/// tentativo (C8), versione del profilo nella riga del registro costi (D2). Dopo il primo token,
/// niente secondo modello: errore, costo dei token generati, fallimento sul circuit breaker.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class HybridLlmServiceAnswerTests
{
    private const string Primary = "deepseek-flash";
    private const string Reserve = "meta-llama/llama-3.3-70b-instruct";

    private static readonly Guid VersionId = Guid.Parse("6f2c0c5e-3c1a-4c3e-9a7e-1f6a2b9d4e10");

    private readonly Mock<ILlmClient> _deepSeek = Client("DeepSeek", Primary);
    private readonly Mock<ILlmClient> _openRouter = Client("OpenRouter", Reserve);
    private readonly Mock<ILlmProviderSelector> _selector = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakers = new();
    private readonly Mock<ILlmCostService> _costService = new();
    private readonly Mock<ILlmBudgetGate> _gate = new();
    private readonly Mock<ILlmCostCalculator> _calculator = new();
    private readonly List<LlmCompletionResult> _loggedCosts = [];

    public HybridLlmServiceAnswerTests()
    {
        Streams(_deepSeek, Primary, "DeepSeek", "Five", " cards.");
        Streams(_openRouter, Reserve, "OpenRouter", "Reserve", " answer.");
        Answers(_deepSeek, Primary, "DeepSeek");
        Answers(_openRouter, Reserve, "OpenRouter");

        _gate.Setup(g => g.AdmitAsync(It.IsAny<LlmBudgetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Admission(LlmBudgetRejection.None));
        _circuitBreakers.Setup(c => c.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        _costService
            .Setup(c => c.LogSuccessAsync(It.IsAny<LlmCompletionResult>(), It.IsAny<LlmUserContext>(), It.IsAny<long>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<LlmCompletionResult, LlmUserContext, long, RequestSource, CancellationToken>((r, _, _, _, _) => _loggedCosts.Add(r))
            .Returns(Task.CompletedTask);
        _calculator
            .Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<string, string, int, int>((model, provider, prompt, completion) => new LlmCostCalculation
            {
                ModelId = model,
                Provider = provider,
                PromptTokens = prompt,
                CompletionTokens = completion,
                InputCost = prompt * 0.000001m,
                OutputCost = completion * 0.000002m,
            });
    }

    private HybridLlmService CreateSut() =>
        new([_deepSeek.Object, _openRouter.Object], _selector.Object, _circuitBreakers.Object, _costService.Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _gate.Object, costCalculator: _calculator.Object);

    private static LlmAnswerRequest Request(string? fallback = Reserve) =>
        new("You answer board game rules.", "How many cards?", Primary, fallback,
            Temperature: 0.7, MaxTokens: 900, ProfileVersionId: VersionId);

    // ─── Parametri dal chiamante ────────────────────────────────────────────────

    [Fact]
    public async Task AnswerStream_SendsTheModelTemperatureAndLengthOfTheRequest()
    {
        await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(
            Primary, It.IsAny<string>(), It.IsAny<string>(), 0.7, 900, It.IsAny<CancellationToken>()), Times.Once);
        _gate.Verify(g => g.AdmitAsync(
            It.Is<LlmBudgetRequest>(r => r.ModelId == Primary && r.MaxOutputTokens == 900 && r.Source == RequestSource.RagPipeline),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Answer_SendsTheModelTemperatureAndLengthOfTheRequest()
    {
        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Success.Should().BeTrue();
        result.ProfileVersionId.Should().Be(VersionId);
        _deepSeek.Verify(c => c.GenerateCompletionAsync(
            Primary, It.IsAny<string>(), It.IsAny<string>(), 0.7, 900, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Fallback prima del primo token, circuit breaker in streaming ───────────

    [Fact]
    public async Task AnswerStream_PrimaryEndsEmpty_AnswersWithTheReserve_AndBothAttemptsReachTheBreaker()
    {
        Streams(_deepSeek, Primary, "DeepSeek");

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
        chunks.Should().NotContain(c => c.ErrorCode != null);
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess("OpenRouter", Reserve, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
    }

    [Fact]
    public async Task AnswerStream_PrimaryThrowsBeforeTheFirstToken_AnswersWithTheReserve()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => Failing(afterTokens: []));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
    }

    [Fact]
    public async Task AnswerStream_PrimaryCircuitOpen_IsNotCalled_AndTheReserveAnswers()
    {
        _circuitBreakers.Setup(c => c.GetState("DeepSeek")).Returns(CircuitState.Open);

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnswerStream_Completed_RecordsSuccess_AndLogsTheCostWithTheProfileVersion()
    {
        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Five cards.");
        _selector.Verify(s => s.RecordSuccess("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        var logged = _loggedCosts.Should().ContainSingle().Which;
        logged.Success.Should().BeTrue();
        logged.ProfileVersionId.Should().Be(VersionId);
        logged.Cost.ModelId.Should().Be(Primary);
    }

    // ─── Dopo il primo token ────────────────────────────────────────────────────

    [Fact]
    public async Task AnswerStream_InterruptedAfterTheFirstToken_EndsWithAnError_WithoutTryingTheReserve()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => Failing(afterTokens: ["Each player draws five"]));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        // Il testo già inviato resta, l'ultimo chunk dice che la risposta non è completa.
        TextOf(chunks).Should().Be("Each player draws five");
        var last = chunks[^1];
        last.IsFinal.Should().BeTrue();
        last.ErrorCode.Should().Be(LlmAnswerErrorCodes.StreamInterrupted);

        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Never);

        // I token generati sono stati fatturati: la riga li registra, con l'errore e la versione.
        var logged = _loggedCosts.Should().ContainSingle().Which;
        logged.Success.Should().BeFalse();
        logged.ErrorMessage.Should().Contain("interrupted");
        logged.Usage.CompletionTokens.Should().BePositive();
        logged.Cost.TotalCost.Should().BePositive();
        logged.ProfileVersionId.Should().Be(VersionId);
        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), It.Is<decimal>(c => c > 0m), true), Times.Once);
    }

    // ─── Nessun modello, tetto, catalogo, degrado ───────────────────────────────

    [Fact]
    public async Task AnswerStream_NoModelAnswers_EndsWithProviderUnavailable_NotWithAnEmptyStream()
    {
        Streams(_deepSeek, Primary, "DeepSeek");
        Streams(_openRouter, Reserve, "OpenRouter");

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        var only = chunks.Should().ContainSingle().Which;
        only.ErrorCode.Should().Be(LlmAnswerErrorCodes.ProviderUnavailable);
        only.ErrorMessage.Should().NotContainAny("DeepSeek", "OpenRouter", "Streaming ended");
    }

    [Fact]
    public async Task AnswerStream_WithoutAReserve_DoesNotInventOne()
    {
        Streams(_deepSeek, Primary, "DeepSeek");

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request(fallback: null)));

        chunks.Should().ContainSingle().Which.ErrorCode.Should().Be(LlmAnswerErrorCodes.ProviderUnavailable);
        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnswerStream_OverTheCap_IsRefused_WithoutTryingTheReserve()
    {
        Refuse(Primary, LlmBudgetRejection.DailyCapReached);

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        var only = chunks.Should().ContainSingle().Which;
        only.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnswerStream_PrimaryOutsideTheCatalog_IsAnsweredByTheReserve()
    {
        Refuse(Primary, LlmBudgetRejection.ModelNotInCatalog);

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
    }

    [Fact]
    public async Task AnswerStream_InDegrade_StartsWithACheaperReserve()
    {
        _gate.Setup(g => g.IsDegradedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _gate.Setup(g => g.IsCheaperReserveAsync(Primary, "DeepSeek", Reserve, "OpenRouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Risposta unica ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Answer_PrimaryFails_AnswersWithTheReserve_AndBothAttemptsReachTheBreaker()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("DeepSeek 503"));

        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Success.Should().BeTrue();
        result.Response.Should().Be("answer from OpenRouter");
        result.ProfileVersionId.Should().Be(VersionId);
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess("OpenRouter", Reserve, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
        _loggedCosts.Should().ContainSingle().Which.ProfileVersionId.Should().Be(VersionId);
    }

    [Fact]
    public async Task Answer_NoModelAnswers_FailsWithProviderUnavailable()
    {
        foreach (var (client, model) in new[] { (_deepSeek, Primary), (_openRouter, Reserve) })
        {
            client
                .Setup(c => c.GenerateCompletionAsync(model, It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("connection refused"));
        }

        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmAnswerErrorCodes.ProviderUnavailable);
        result.ErrorMessage.Should().NotContain("connection refused", "il dettaglio del provider resta nei log");
    }

    [Fact]
    public async Task Answer_OverTheCap_IsRefused_WithoutTryingTheReserve()
    {
        Refuse(Primary, LlmBudgetRejection.DailyCapReached);

        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        _openRouter.Verify(c => c.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static Mock<ILlmClient> Client(string provider, string model)
    {
        var client = new Mock<ILlmClient>();
        client.Setup(c => c.ProviderName).Returns(provider);
        client.Setup(c => c.SupportsModel(model)).Returns(true);
        return client;
    }

    private static void Streams(Mock<ILlmClient> client, string model, string provider, params string[] tokens) =>
        client
            .Setup(c => c.GenerateCompletionStreamAsync(model, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => Completed(model, provider, tokens));

    private static void Answers(Mock<ILlmClient> client, string model, string provider) =>
        client
            .Setup(c => c.GenerateCompletionAsync(model, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                $"answer from {provider}",
                new LlmUsage(100, 20, 120),
                new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider }));

    /// <summary>Uno stream completo; senza token, uno stream vuoto, come un client che ha fallito in silenzio.</summary>
    private static async IAsyncEnumerable<StreamChunk> Completed(
        string model, string provider, string[] tokens, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        if (tokens.Length == 0)
        {
            yield break;
        }

        foreach (var token in tokens)
        {
            yield return new StreamChunk(token);
        }

        yield return new StreamChunk(
            null,
            new LlmUsage(100, tokens.Length, 100 + tokens.Length),
            new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = model, Provider = provider },
            IsFinal: true,
            FinishReason: "stop");
    }

    /// <summary>Uno stream che emette dei token e poi si rompe, come una connessione chiusa a metà.</summary>
    private static async IAsyncEnumerable<StreamChunk> Failing(
        string[] afterTokens, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var token in afterTokens)
        {
            yield return new StreamChunk(token);
        }

        throw new IOException("The response ended prematurely.");
    }

    private static LlmBudgetAdmission Admission(LlmBudgetRejection rejection, string? message = null) => new()
    {
        ReservationId = rejection == LlmBudgetRejection.None ? Guid.NewGuid() : Guid.Empty,
        Rejection = rejection,
        BudgetClass = LlmBudgetClass.Answer,
        EstimatedCostUsd = 0.004m,
        Message = message,
    };

    private void Refuse(string model, LlmBudgetRejection rejection) =>
        _gate.Setup(g => g.AdmitAsync(It.Is<LlmBudgetRequest>(r => r.ModelId == model), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Admission(rejection, $"refused: {rejection}"));

    private static async Task<List<StreamChunk>> ReadAsync(IAsyncEnumerable<StreamChunk> stream)
    {
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in stream)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static string TextOf(IEnumerable<StreamChunk> chunks) => string.Concat(chunks.Select(c => c.Content));
}
