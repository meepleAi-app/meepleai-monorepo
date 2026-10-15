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

    // ─── Il consumatore smette di aspettare ─────────────────────────────────────

    /// <summary>
    /// I client non lanciano sull'annullamento: escono dal ciclo e lo stream sembra finito. Se il
    /// gateway lo leggesse come esito, un utente che chiude la chat aprirebbe il circuito a tutti
    /// (vuoto prima del primo token) o registrerebbe un successo (troncato dopo).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task AnswerStream_CancelledByTheConsumer_IsNeitherAProviderFailureNorASuccess(int tokenCount)
    {
        string[] tokensBeforeCancel = tokenCount == 0 ? [] : ["Each player"];
        using var cts = new CancellationTokenSource();
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => QuietlyCancelled(cts, tokensBeforeCancel));

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in CreateSut().GenerateAnswerStreamAsync(Request(), cts.Token))
        {
            chunks.Add(chunk);
        }

        TextOf(chunks).Should().Be(string.Concat(tokensBeforeCancel));
        chunks.Should().NotContain(c => c.ErrorCode != null, "nessuno legge più lo stream");
        _selector.Verify(s => s.RecordFailure(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Never);
        _selector.Verify(s => s.RecordSuccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Never);
        _openRouter.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), It.IsAny<decimal>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Answer_CancelledByTheConsumer_IsNotAProviderFailure()
    {
        using var cts = new CancellationTokenSource();
        _deepSeek
            .Setup(c => c.GenerateCompletionAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return LlmCompletionResult.CreateFailure("Request timeout");
            });

        var act = () => CreateSut().GenerateAnswerAsync(Request(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _selector.Verify(s => s.RecordFailure(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Never);
        _openRouter.Verify(c => c.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), 0m, It.IsAny<bool>()), Times.Once);
    }

    /// <summary>
    /// Un conto che non si chiude lascia la prenotazione nel gate fino al riavvio: il dispose di un
    /// client che lancia, o un calcolo del costo che lancia, non devono impedirlo.
    /// </summary>
    [Fact]
    public async Task AnswerStream_WhenRecordingTheCostThrows_TheGateIsStillSettledOnce()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => Failing(afterTokens: ["Each player"]));
        _calculator
            .Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("catalog unreadable"));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        chunks[^1].ErrorCode.Should().Be(LlmAnswerErrorCodes.StreamInterrupted);
        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), 0.004m, false), Times.Once);
    }

    [Fact]
    public async Task AnswerStream_WhenTheProviderStreamThrowsOnDispose_TheAnswerStillCompletes()
    {
        var stream = new Mock<IAsyncEnumerable<StreamChunk>>();
        var enumerator = new Mock<IAsyncEnumerator<StreamChunk>>();
        var chunks = new Queue<StreamChunk>([
            new StreamChunk("Five."),
            new StreamChunk(null, new LlmUsage(10, 2, 12),
                new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = Primary, Provider = "DeepSeek" }, IsFinal: true),
        ]);
        enumerator.Setup(e => e.MoveNextAsync()).Returns(() => ValueTask.FromResult(chunks.Count > 0));
        enumerator.Setup(e => e.Current).Returns(() => chunks.Dequeue());
        enumerator.Setup(e => e.DisposeAsync()).Throws(new ObjectDisposedException("socket"));
        stream.Setup(s => s.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(enumerator.Object);
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(stream.Object);

        var read = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(read).Should().Be("Five.");
        _selector.Verify(s => s.RecordSuccess("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Once);
        _gate.Verify(g => g.Complete(It.IsAny<LlmBudgetAdmission>(), It.IsAny<decimal>(), It.IsAny<bool>()), Times.Once);
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

    // ─── Risposta pagata senza testo (#4381) ────────────────────────────────────
    // Il modello consuma i token e non scrive niente (deepseek-flash con il thinking attivo che
    // esaurisce max_tokens). Non c'è un primo token, quindi vale la regola di prima del primo
    // token: guasto sul circuit breaker e riserva. Ma i token sono stati pagati: il costo si registra.

    [Fact]
    public async Task AnswerStream_PrimaryPaysForNoText_AnswersWithTheReserve_AndLogsThePaidCost()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => PaidWithoutText(Primary, "DeepSeek"));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Reserve answer.");
        chunks.Should().NotContain(c => c.ErrorCode != null);
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _selector.Verify(s => s.RecordSuccess("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult>()), Times.Never);
        _loggedCosts.Should().Contain(r => r.Cost.ModelId == Primary && r.Usage.CompletionTokens == 1500,
            "i token del tentativo vuoto sono stati pagati");
    }

    [Fact]
    public async Task AnswerStream_NoModelWritesText_EndsWithProviderUnavailable_NotWithAnEmptyAnswer()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionStreamAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(() => PaidWithoutText(Primary, "DeepSeek"));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request(fallback: null)));

        chunks.Should().ContainSingle().Which.ErrorCode.Should().Be(LlmAnswerErrorCodes.ProviderUnavailable,
            "uno stream che finisce senza testo e senza errore la pipeline lo chiuderebbe come una risposta completa e vuota");
    }

    [Fact]
    public async Task Answer_PrimaryPaysForNoText_AnswersWithTheReserve_AndLogsThePaidCost()
    {
        _deepSeek
            .Setup(c => c.GenerateCompletionAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                string.Empty,
                new LlmUsage(1520, 1500, 3020),
                new LlmCost { InputCost = 0.000456m, OutputCost = 0.0018m, ModelId = Primary, Provider = "DeepSeek" }));

        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Success.Should().BeTrue();
        result.Response.Should().Be("answer from OpenRouter");
        _selector.Verify(s => s.RecordFailure("DeepSeek", Primary, It.IsAny<long>(), It.IsAny<LlmCompletionResult?>()), Times.Once);
        _loggedCosts.Should().Contain(r => r.Cost.ModelId == Primary && r.Usage.CompletionTokens == 1500);
    }

    // ─── Ragionamento spento nelle risposte (#4381) ─────────────────────────────

    [Fact]
    public async Task AnswerStream_AClientThatCanSkipReasoning_AnswersWithoutIt()
    {
        var reasoning = _deepSeek.As<ILlmReasoningSwitch>();
        reasoning
            .Setup(c => c.GenerateCompletionStreamWithoutReasoningAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                0.7, 900, It.IsAny<CancellationToken>()))
            .Returns(() => Completed(Primary, "DeepSeek", ["Five", " cards."]));

        var chunks = await ReadAsync(CreateSut().GenerateAnswerStreamAsync(Request()));

        TextOf(chunks).Should().Be("Five cards.");
        reasoning.Verify(c => c.GenerateCompletionStreamWithoutReasoningAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
            0.7, 900, It.IsAny<CancellationToken>()), Times.Once);
        _deepSeek.Verify(c => c.GenerateCompletionStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Answer_AClientThatCanSkipReasoning_AnswersWithoutIt()
    {
        var reasoning = _deepSeek.As<ILlmReasoningSwitch>();
        reasoning
            .Setup(c => c.GenerateCompletionWithoutReasoningAsync(Primary, It.IsAny<string>(), It.IsAny<string>(),
                0.7, 900, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "answer without reasoning",
                new LlmUsage(100, 20, 120),
                new LlmCost { InputCost = 0.0001m, OutputCost = 0.0002m, ModelId = Primary, Provider = "DeepSeek" }));

        var result = await CreateSut().GenerateAnswerAsync(Request());

        result.Response.Should().Be("answer without reasoning");
        _deepSeek.Verify(c => c.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
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

    /// <summary>
    /// #4381: lo stream di un modello che ha speso tutti i token senza scrivere testo — il solo chunk
    /// finale, con l'uso pagato e <c>finish_reason = length</c>, com'è arrivato da deepseek-flash.
    /// </summary>
    private static async IAsyncEnumerable<StreamChunk> PaidWithoutText(
        string model, string provider, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return new StreamChunk(
            null,
            new LlmUsage(1520, 1500, 3020),
            new LlmCost { InputCost = 0.000456m, OutputCost = 0.0018m, ModelId = model, Provider = provider },
            IsFinal: true,
            FinishReason: "length");
    }

    /// <summary>
    /// Come i client veri su un annullamento: emettono quel che hanno, poi escono dal ciclo senza
    /// lanciare (OpenRouter legge finché <c>!ct.IsCancellationRequested</c>).
    /// </summary>
    private static async IAsyncEnumerable<StreamChunk> QuietlyCancelled(CancellationTokenSource cts, string[] tokens)
    {
        await Task.Yield();
        foreach (var token in tokens)
        {
            yield return new StreamChunk(token);
        }

        await cts.CancelAsync();
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
