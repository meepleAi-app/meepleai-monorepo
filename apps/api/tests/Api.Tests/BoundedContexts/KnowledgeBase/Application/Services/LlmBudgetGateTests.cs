using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4166 (ADR-095 D4, D5 e D6): il tetto di spesa del gateway LLM, verificato prima della chiamata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4166")]
public sealed class LlmBudgetGateTests : IDisposable
{
    private const string PaidModel = "paid-model";
    private const string FreeModel = "free-model";
    private const string UnknownModel = "unknown-model";

    /// <summary>1 $ per milione in ingresso, 2 $ in uscita: i conti dei test restano leggibili.</summary>
    private static readonly LlmModelPricing PaidPricing = new()
    {
        ModelId = PaidModel, Provider = "OpenRouter", InputCostPer1M = 1m, OutputCostPer1M = 2m,
    };

    private static readonly DateTimeOffset Now = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<ILlmCostCalculator> _calculator = new();
    private readonly Mock<ILlmSystemConfigProvider> _config = new();
    private readonly Mock<ILlmCostLogRepository> _costLog = new();
    private readonly ServiceProvider _services;
    private readonly LlmBudgetGate _gate;
    private List<LlmSpendBySourceAndDay> _spend = [];
    private int _spendReads;

    public LlmBudgetGateTests()
    {
        _calculator.Setup(c => c.ResolvePricingAsync(PaidModel, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaidPricing);
        _calculator.Setup(c => c.ResolvePricingAsync(FreeModel, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmModelPricing.Free with { ModelId = FreeModel, Provider = "Ollama" });
        _calculator.Setup(c => c.ResolvePricingAsync(UnknownModel, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModelPricing?)null);
        UsePolicy(dailyCap: 1m, monthlyCap: 20m);
        _costLog.Setup(r => r.GetSpendBySourceAndDayAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _spendReads++;
                return _spend.ToList();
            });

        _services = new ServiceCollection().AddScoped(_ => _costLog.Object).BuildServiceProvider();
        _gate = new LlmBudgetGate(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _config.Object,
            _calculator.Object,
            _time,
            NullLogger<LlmBudgetGate>.Instance);
    }

    public void Dispose()
    {
        _gate.Dispose();
        _services.Dispose();
    }

    private void UsePolicy(decimal dailyCap, decimal monthlyCap, int background = 30, int evaluation = 20) =>
        _config.Setup(c => c.GetBudgetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmBudgetPolicy(dailyCap, monthlyCap, 80, background, evaluation));

    private void Spent(RequestSource source, decimal amount, DateOnly? day = null) =>
        _spend.Add(new LlmSpendBySourceAndDay(source.ToString(), day ?? Today, amount));

    /// <summary>3000 caratteri e 1500 token in uscita: 1000 + 1500 × 2 = 0,004 $ al listino di prova.</summary>
    private static LlmBudgetRequest Call(RequestSource source = RequestSource.Manual, string model = PaidModel) =>
        new(source, model, "OpenRouter", PromptCharacters: 3000, MaxOutputTokens: 1500);

    private Task<LlmBudgetAdmission> AdmitAsync(LlmBudgetRequest request) =>
        _gate.AdmitAsync(request, TestContext.Current.CancellationToken);

    [Fact]
    public void Estimate_CountsThePromptTheImagesAndTheMaximumAnswer()
    {
        var estimate = LlmBudgetGate.Estimate(PaidPricing, Call() with { ImageCount = 1 });

        // ingresso: 3000 / 3 + 1500 per l'immagine = 2500 token a 1 $; uscita: 1500 token a 2 $
        estimate.Should().Be((2500m * 1m + 1500m * 2m) / 1_000_000m);
    }

    [Fact]
    public async Task AModelTheCatalogDoesNotPrice_IsRefused_FailClosed()
    {
        var admission = await AdmitAsync(Call(model: UnknownModel));

        admission.IsAllowed.Should().BeFalse("registrata a $0, la chiamata sfuggirebbe al tetto (D5)");
        admission.Rejection.Should().Be(LlmBudgetRejection.ModelNotInCatalog);
        admission.ErrorCode.Should().Be(LlmBudgetAdmission.ModelErrorCode);
        admission.Message.Should().Contain(UnknownModel);
    }

    [Fact]
    public async Task AFreeModel_Passes_EvenOverTheCap()
    {
        Spent(RequestSource.Manual, 5m);

        var admission = await AdmitAsync(Call(model: FreeModel));

        admission.IsAllowed.Should().BeTrue("un modello gratuito non spende");
        _spendReads.Should().Be(0, "per un modello gratuito non serve leggere la spesa");
    }

    [Fact]
    public async Task UnderTheDailyCap_IsAllowed()
    {
        Spent(RequestSource.Manual, 0.99m);

        var admission = await AdmitAsync(Call());

        admission.IsAllowed.Should().BeTrue();
        admission.EstimatedCostUsd.Should().Be(0.004m);
    }

    [Fact]
    public async Task OverTheDailyCap_IsRefused_WithTheDailyCode()
    {
        Spent(RequestSource.Manual, 0.997m);

        var admission = await AdmitAsync(Call());

        admission.IsAllowed.Should().BeFalse("0,997 + 0,004 supera il tetto di 1 $");
        admission.Rejection.Should().Be(LlmBudgetRejection.DailyCapReached);
        admission.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
    }

    [Fact]
    public async Task OverTheMonthlyCap_IsRefused_WithTheMonthlyCode()
    {
        Spent(RequestSource.Manual, 19.998m, Today.AddDays(-1));

        var admission = await AdmitAsync(Call());

        admission.Rejection.Should().Be(LlmBudgetRejection.MonthlyCapReached);
        admission.ErrorCode.Should().Be(LlmBudgetAdmission.MonthlyBudgetErrorCode);
    }

    [Fact]
    public async Task TwoConcurrentCalls_DoNotPassOnTheSameMargin()
    {
        Spent(RequestSource.Manual, 0.993m);

        var first = await AdmitAsync(Call());
        var second = await AdmitAsync(Call());

        first.IsAllowed.Should().BeTrue();
        second.IsAllowed.Should().BeFalse("la prima riserva 0,004 $: 0,993 + 0,004 + 0,004 supera il tetto");
    }

    [Fact]
    public async Task ClosingACall_ReplacesTheReservationWithTheActualCost()
    {
        Spent(RequestSource.Manual, 0.993m);
        var first = await AdmitAsync(Call());

        _gate.Complete(first, actualCostUsd: 0.001m);
        var second = await AdmitAsync(Call());

        second.IsAllowed.Should().BeTrue("0,993 + 0,001 reale + 0,004 stimato sta nel tetto; con la riserva ancora aperta no");
    }

    [Fact]
    public async Task AFailedCall_FreesItsReservation()
    {
        Spent(RequestSource.Manual, 0.993m);
        var first = await AdmitAsync(Call());

        _gate.Complete(first, actualCostUsd: 0m);
        var second = await AdmitAsync(Call());

        second.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Background_IsCappedByItsQuota_WhileAnswersStillPass()
    {
        Spent(RequestSource.ChunkTranslation, 0.298m); // la quota del background è il 30% di 1 $

        var background = await AdmitAsync(Call(RequestSource.ChunkTranslation));
        var answer = await AdmitAsync(Call(RequestSource.Manual));

        background.Rejection.Should().Be(LlmBudgetRejection.DailyQuotaReached);
        background.BudgetClass.Should().Be(LlmBudgetClass.Background);
        answer.IsAllowed.Should().BeTrue("il background non consuma il budget delle risposte");
    }

    [Fact]
    public async Task Evaluations_AreCappedByTheirOwnQuota()
    {
        Spent(RequestSource.Evaluation, 0.198m, Today.AddDays(-2));
        UsePolicy(dailyCap: 1m, monthlyCap: 1m);

        var evaluation = await AdmitAsync(Call(RequestSource.Evaluation));

        evaluation.Rejection.Should().Be(LlmBudgetRejection.MonthlyQuotaReached);
        evaluation.BudgetClass.Should().Be(LlmBudgetClass.Evaluation);
    }

    [Fact]
    public async Task TheSpend_IsReadAgainOnlyAfterTheTtl()
    {
        Spent(RequestSource.Manual, 0.5m);
        (await AdmitAsync(Call())).IsAllowed.Should().BeTrue();

        Spent(RequestSource.Manual, 0.499m); // scritto da un'altra istanza o da un job
        (await AdmitAsync(Call())).IsAllowed.Should().BeTrue("la lettura precedente vale ancora");
        _spendReads.Should().Be(1);

        _time.Advance(LlmBudgetGate.SpendSnapshotTtl);
        var afterTtl = await AdmitAsync(Call());

        _spendReads.Should().Be(2);
        afterTtl.IsAllowed.Should().BeFalse("ora la spesa letta è 0,999 $");
    }

    [Fact]
    public async Task ACostAlreadyInTheLog_IsNotCountedTwice()
    {
        Spent(RequestSource.Manual, 0.5m);
        var call = await AdmitAsync(Call());
        _gate.Complete(call, actualCostUsd: 0.49m);

        _time.Advance(LlmBudgetGate.SpendSnapshotTtl);
        Spent(RequestSource.Manual, 0.49m); // la riga della chiamata chiusa ora è nel registro
        var next = await AdmitAsync(Call());

        next.IsAllowed.Should().BeTrue("0,99 + 0,004 sta nel tetto: contare due volte gli 0,49 lo supererebbe");
    }

    [Fact]
    public async Task AtMidnight_YesterdaysSpend_LeavesTheDailyCap()
    {
        Spent(RequestSource.Manual, 0.999m);
        (await AdmitAsync(Call())).IsAllowed.Should().BeFalse();

        _time.SetUtcNow(new DateTimeOffset(2026, 10, 12, 0, 0, 1, TimeSpan.Zero));
        var tomorrow = await AdmitAsync(Call());

        tomorrow.IsAllowed.Should().BeTrue("la spesa di ieri conta solo per il mese");
    }

    [Fact]
    public async Task WithoutAnySpendReading_TheCallIsRefused()
    {
        _costLog.Setup(r => r.GetSpendBySourceAndDayAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("database down"));

        var admission = await AdmitAsync(Call());

        admission.Rejection.Should().Be(LlmBudgetRejection.BudgetUnavailable, "senza la spesa il tetto non è verificabile");
        admission.ErrorCode.Should().Be(LlmBudgetAdmission.UnavailableErrorCode);
    }

    [Fact]
    public async Task WhenTheSpendCannotBeReadAgain_TodaysPreviousReadingIsReused()
    {
        Spent(RequestSource.Manual, 0.5m);
        (await AdmitAsync(Call())).IsAllowed.Should().BeTrue();
        _costLog.Setup(r => r.GetSpendBySourceAndDayAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("database down"));

        _time.Advance(LlmBudgetGate.SpendSnapshotTtl);
        var admission = await AdmitAsync(Call());

        admission.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task WhenTheCatalogCannotBeRead_TheCallIsRefused()
    {
        _calculator.Setup(c => c.ResolvePricingAsync(PaidModel, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The model price list could not be loaded"));

        var admission = await AdmitAsync(Call());

        admission.Rejection.Should().Be(LlmBudgetRejection.BudgetUnavailable);
    }

    [Fact]
    public async Task ARefusedAdmission_LeavesNoReservationBehind()
    {
        Spent(RequestSource.Manual, 0.997m);
        var refused = await AdmitAsync(Call());
        _gate.Complete(refused, actualCostUsd: 0m);
        UsePolicy(dailyCap: 1.005m, monthlyCap: 20m);

        var next = await AdmitAsync(Call());

        next.IsAllowed.Should().BeTrue("il rifiuto non deve aver riservato nulla");
    }
}
