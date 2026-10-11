using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Observability;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ADR-095 D4 e D6 (#4166): il tetto di spesa del gateway LLM, verificato prima di ogni chiamata.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stima.</b> Il costo massimo di una chiamata è il prompt contato in token più la lunghezza
/// massima della risposta, ai prezzi del catalogo. È conservativa per costruzione: il tetto scatta
/// un po' prima del consumo reale, come ADR-095 mette in conto.
/// </para>
/// <para>
/// <b>Spesa.</b> Il consumo del giorno e del mese viene dal registro costi, per sorgente, e si
/// rilegge ogni <see cref="SpendSnapshotTtl"/>. Fra una lettura e l'altra il gate somma da sé le
/// riserve delle chiamate in corso e i costi reali delle chiamate chiuse, così due chiamate
/// concorrenti non passano sullo stesso margine. Il conteggio è in memoria: vale per una sola
/// istanza dell'API, che è il contratto di staging (ADR-087).
/// </para>
/// <para>
/// <b>Fail closed.</b> Un modello che il catalogo non prezza viene rifiutato: registrato a $0,
/// sfuggirebbe al tetto (D5). Se catalogo o spesa non si leggono, la chiamata non parte, salvo che
/// esista una lettura della spesa dello stesso giorno, che si riusa.
/// </para>
/// <para>
/// Una chiamata a un modello gratuito (Ollama, o un modello che il catalogo prezza a zero) passa
/// sempre: non spende.
/// </para>
/// </remarks>
internal sealed class LlmBudgetGate : ILlmBudgetGate, IDisposable
{
    /// <summary>Ogni quanto si rilegge la spesa dal registro costi.</summary>
    internal static readonly TimeSpan SpendSnapshotTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Caratteri per token nella stima dell'ingresso. Tre e non i circa quattro dell'inglese: i
    /// prompt in italiano e in tedesco ne hanno meno per token, e la stima deve stare per eccesso.
    /// </summary>
    internal const int CharactersPerToken = 3;

    /// <summary>Token stimati per un'immagine in una chiamata multimodale.</summary>
    internal const int TokensPerImage = 1500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILlmSystemConfigProvider _configProvider;
    private readonly ILlmCostCalculator _costCalculator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LlmBudgetGate> _logger;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, Reservation> _reservations = [];
    private readonly List<Settlement> _settled = [];
    private SpendSnapshot? _snapshot;

    public LlmBudgetGate(
        IServiceScopeFactory scopeFactory,
        ILlmSystemConfigProvider configProvider,
        ILlmCostCalculator costCalculator,
        TimeProvider timeProvider,
        ILogger<LlmBudgetGate> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _costCalculator = costCalculator ?? throw new ArgumentNullException(nameof(costCalculator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<LlmBudgetAdmission> AdmitAsync(LlmBudgetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var budgetClass = LlmBudgetClassification.For(request.Source);

        LlmModelPricing? pricing;
        try
        {
            pricing = await _costCalculator.ResolvePricingAsync(request.ModelId, request.Provider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "The AI model catalog could not be read: the LLM call to {Model} is refused", request.ModelId);
            return Reject(request, budgetClass, LlmBudgetRejection.BudgetUnavailable, 0m,
                "The AI budget cannot be checked right now, so the call was not made.");
        }

        if (pricing is null)
        {
            return Reject(request, budgetClass, LlmBudgetRejection.ModelNotInCatalog, 0m,
                $"Model {request.ModelId} is not in the AI model catalog, so its cost cannot be checked. Add it to the catalog to use it.");
        }

        var estimate = Estimate(pricing, request);
        if (estimate == 0m)
        {
            return Allowed(Guid.Empty, budgetClass, 0m);
        }

        var policy = await _configProvider.GetBudgetPolicyAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Reject(request, budgetClass, LlmBudgetRejection.BudgetUnavailable, estimate,
                "The AI budget cannot be checked right now, so the call was not made.");
        }

        lock (_lock)
        {
            var spend = Totals(snapshot, budgetClass);
            var rejection = Check(policy, budgetClass, spend, estimate);
            if (rejection != LlmBudgetRejection.None)
            {
                return Reject(request, budgetClass, rejection, estimate, MessageFor(rejection, budgetClass), policy, spend);
            }

            var reservationId = Guid.NewGuid();
            _reservations[reservationId] = new Reservation(budgetClass, estimate);
            return Allowed(reservationId, budgetClass, estimate);
        }
    }

    /// <inheritdoc/>
    public void Complete(LlmBudgetAdmission admission, decimal actualCostUsd)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (!admission.IsAllowed)
        {
            return;
        }

        lock (_lock)
        {
            if (admission.ReservationId != Guid.Empty)
            {
                _reservations.Remove(admission.ReservationId);
            }

            if (actualCostUsd > 0m)
            {
                _settled.Add(new Settlement(admission.BudgetClass, actualCostUsd, _timeProvider.GetUtcNow()));
            }
        }
    }

    public void Dispose() => _loadGate.Dispose();

    /// <summary>Il costo massimo della chiamata, in USD.</summary>
    internal static decimal Estimate(LlmModelPricing pricing, LlmBudgetRequest request)
    {
        var inputTokens = ((long)request.PromptCharacters + CharactersPerToken - 1) / CharactersPerToken
            + (long)request.ImageCount * TokensPerImage;
        return (inputTokens * pricing.InputCostPer1M + request.MaxOutputTokens * pricing.OutputCostPer1M) / 1_000_000m;
    }

    private static LlmBudgetRejection Check(LlmBudgetPolicy policy, LlmBudgetClass budgetClass, Spend spend, decimal estimate)
    {
        if (spend.Day + estimate > policy.DailyCapUsd)
        {
            return LlmBudgetRejection.DailyCapReached;
        }

        if (spend.Month + estimate > policy.MonthlyCapUsd)
        {
            return LlmBudgetRejection.MonthlyCapReached;
        }

        var quotaPercent = budgetClass switch
        {
            LlmBudgetClass.Background => policy.BackgroundQuotaPercent,
            LlmBudgetClass.Evaluation => policy.EvaluationQuotaPercent,
            _ => (int?)null,
        };
        if (quotaPercent is not { } percent)
        {
            return LlmBudgetRejection.None;
        }

        if (spend.ClassDay + estimate > policy.DailyCapUsd * percent / 100m)
        {
            return LlmBudgetRejection.DailyQuotaReached;
        }

        if (spend.ClassMonth + estimate > policy.MonthlyCapUsd * percent / 100m)
        {
            return LlmBudgetRejection.MonthlyQuotaReached;
        }

        return LlmBudgetRejection.None;
    }

    private static string MessageFor(LlmBudgetRejection rejection, LlmBudgetClass budgetClass) => rejection switch
    {
        LlmBudgetRejection.DailyCapReached =>
            "The AI service is paused: today's AI budget has been reached. It resumes tomorrow (UTC).",
        LlmBudgetRejection.MonthlyCapReached =>
            "The AI service is paused: this month's AI budget has been reached.",
        LlmBudgetRejection.DailyQuotaReached =>
            $"The {budgetClass} share of today's AI budget has been used up. It resumes tomorrow (UTC).",
        LlmBudgetRejection.MonthlyQuotaReached =>
            $"The {budgetClass} share of this month's AI budget has been used up.",
        _ => "The AI call was not made.",
    };

    /// <summary>La spesa contata: registro, più le chiamate chiuse e quelle in corso dopo la lettura.</summary>
    private Spend Totals(SpendSnapshot snapshot, LlmBudgetClass budgetClass)
    {
        var day = snapshot.DayTotal;
        var month = snapshot.MonthTotal;
        var classDay = snapshot.DayByClass.GetValueOrDefault(budgetClass);
        var classMonth = snapshot.MonthByClass.GetValueOrDefault(budgetClass);

        foreach (var settlement in _settled)
        {
            day += settlement.Amount;
            month += settlement.Amount;
            if (settlement.Class == budgetClass)
            {
                classDay += settlement.Amount;
                classMonth += settlement.Amount;
            }
        }

        foreach (var reservation in _reservations.Values)
        {
            day += reservation.Amount;
            month += reservation.Amount;
            if (reservation.Class == budgetClass)
            {
                classDay += reservation.Amount;
                classMonth += reservation.Amount;
            }
        }

        return new Spend(day, month, classDay, classMonth);
    }

    private async Task<SpendSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var current = Volatile.Read(ref _snapshot);
        if (IsFresh(current))
        {
            return current;
        }

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = Volatile.Read(ref _snapshot);
            if (IsFresh(current))
            {
                return current;
            }

            var queriedAt = _timeProvider.GetUtcNow();
            var today = DateOnly.FromDateTime(queriedAt.UtcDateTime);
            try
            {
                IReadOnlyList<LlmSpendBySourceAndDay> rows;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
                    rows = await repository
                        .GetSpendBySourceAndDayAsync(new DateOnly(today.Year, today.Month, 1), today, cancellationToken)
                        .ConfigureAwait(false);
                }

                var fresh = SpendSnapshot.From(today, queriedAt, rows);
                lock (_lock)
                {
                    // I costi chiusi prima della lettura sono già nel registro: contarli ancora li
                    // raddoppierebbe. Quelli chiusi dopo restano finché la prossima lettura non li vede.
                    _settled.RemoveAll(settlement => settlement.At < queriedAt);
                    Volatile.Write(ref _snapshot, fresh);
                }

                return fresh;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The LLM spend could not be read from the cost log");
                return current is not null && current.Today == today ? current : null;
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private bool IsFresh(SpendSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        return snapshot.Today == DateOnly.FromDateTime(now.UtcDateTime) && now - snapshot.QueriedAt < SpendSnapshotTtl;
    }

    private static LlmBudgetAdmission Allowed(Guid reservationId, LlmBudgetClass budgetClass, decimal estimate) => new()
    {
        ReservationId = reservationId,
        Rejection = LlmBudgetRejection.None,
        BudgetClass = budgetClass,
        EstimatedCostUsd = estimate,
    };

    private LlmBudgetAdmission Reject(
        LlmBudgetRequest request,
        LlmBudgetClass budgetClass,
        LlmBudgetRejection rejection,
        decimal estimate,
        string message,
        LlmBudgetPolicy? policy = null,
        Spend? spend = null)
    {
        _logger.LogWarning(
            "LLM call refused before reaching the provider: {Rejection} (source {Source}, class {BudgetClass}, model {Model}, estimate ${Estimate:F6}, spent today ${DaySpend:F4} of ${DailyCap:F2}, this month ${MonthSpend:F4} of ${MonthlyCap:F2})",
            rejection, request.Source, budgetClass, request.ModelId, estimate,
            spend?.Day, policy?.DailyCapUsd, spend?.Month, policy?.MonthlyCapUsd);
        MeepleAiMetrics.LlmBudgetRejectedCalls.Add(1, new TagList
        {
            { "reason", rejection.ToString() },
            { "budget_class", budgetClass.ToString() },
        });

        return new LlmBudgetAdmission
        {
            ReservationId = Guid.Empty,
            Rejection = rejection,
            BudgetClass = budgetClass,
            EstimatedCostUsd = estimate,
            Message = message,
        };
    }

    private sealed record Reservation(LlmBudgetClass Class, decimal Amount);

    private sealed record Settlement(LlmBudgetClass Class, decimal Amount, DateTimeOffset At);

    private sealed record Spend(decimal Day, decimal Month, decimal ClassDay, decimal ClassMonth);

    private sealed record SpendSnapshot(
        DateOnly Today,
        DateTimeOffset QueriedAt,
        decimal DayTotal,
        decimal MonthTotal,
        IReadOnlyDictionary<LlmBudgetClass, decimal> DayByClass,
        IReadOnlyDictionary<LlmBudgetClass, decimal> MonthByClass)
    {
        public static SpendSnapshot From(DateOnly today, DateTimeOffset queriedAt, IReadOnlyList<LlmSpendBySourceAndDay> rows)
        {
            var dayByClass = new Dictionary<LlmBudgetClass, decimal>();
            var monthByClass = new Dictionary<LlmBudgetClass, decimal>();
            decimal day = 0m, month = 0m;
            foreach (var row in rows)
            {
                // Una sorgente che il registro scrive ma l'enum non conosce più conta come
                // background: è la quota più stretta, quindi l'errore sta dalla parte del tetto.
                var budgetClass = Enum.TryParse<RequestSource>(row.RequestSource, out var source)
                    ? LlmBudgetClassification.For(source)
                    : LlmBudgetClass.Background;

                month += row.TotalCostUsd;
                monthByClass[budgetClass] = monthByClass.GetValueOrDefault(budgetClass) + row.TotalCostUsd;
                if (row.Day == today)
                {
                    day += row.TotalCostUsd;
                    dayByClass[budgetClass] = dayByClass.GetValueOrDefault(budgetClass) + row.TotalCostUsd;
                }
            }

            return new SpendSnapshot(today, queriedAt, day, month, dayByClass, monthByClass);
        }
    }
}
