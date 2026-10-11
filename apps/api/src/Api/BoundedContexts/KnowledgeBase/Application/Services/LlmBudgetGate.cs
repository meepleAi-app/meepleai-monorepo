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

    /// <summary>
    /// Dopo una lettura della spesa fallita, quanto si riusa la lettura precedente prima di
    /// riprovare: senza, ogni chiamata ripeterebbe la query contro un database lento.
    /// </summary>
    internal static readonly TimeSpan SpendRetryInterval = TimeSpan.FromSeconds(5);

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
            return Reject(request, budgetClass, LlmBudgetRejection.BudgetUnavailable, 0m, UnavailableMessage);
        }

        if (pricing is null)
        {
            return Reject(request, budgetClass, LlmBudgetRejection.ModelNotInCatalog, 0m,
                $"Il modello {request.ModelId} non è nel catalogo dei modelli AI, quindi il suo costo non si può verificare. Aggiungilo al catalogo per usarlo.");
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
            return Reject(request, budgetClass, LlmBudgetRejection.BudgetUnavailable, estimate, UnavailableMessage);
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
    public void Complete(LlmBudgetAdmission admission, decimal actualCostUsd, bool recordedInCostLog = true)
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
                _settled.Add(new Settlement(admission.BudgetClass, actualCostUsd, _timeProvider.GetUtcNow(), recordedInCostLog));
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

    private const string UnavailableMessage =
        "Il budget AI non si può verificare in questo momento, quindi la chiamata non è partita.";

    /// <summary>
    /// Il messaggio per chi ha fatto la domanda. In italiano, come gli altri messaggi d'errore che il
    /// backend manda agli utenti: ogni consumatore lo può mostrare così com'è.
    /// </summary>
    private static string MessageFor(LlmBudgetRejection rejection, LlmBudgetClass budgetClass) => rejection switch
    {
        LlmBudgetRejection.DailyCapReached =>
            "Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.",
        LlmBudgetRejection.MonthlyCapReached =>
            "Il servizio AI è in pausa fino al mese prossimo: è stato raggiunto il budget di spesa del mese.",
        LlmBudgetRejection.DailyQuotaReached =>
            $"La quota di oggi per {ClassName(budgetClass)} è esaurita: riprende domani.",
        LlmBudgetRejection.MonthlyQuotaReached =>
            $"La quota del mese per {ClassName(budgetClass)} è esaurita.",
        _ => "La chiamata AI non è partita.",
    };

    private static string ClassName(LlmBudgetClass budgetClass) => budgetClass switch
    {
        LlmBudgetClass.Background => "il lavoro in background",
        LlmBudgetClass.Evaluation => "le valutazioni",
        _ => "le risposte",
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
            // Un costo che non arriva nel registro resta qui oltre il giorno in cui è nato: conta per
            // il mese, e per il giorno solo se è di oggi.
            var settledToday = DateOnly.FromDateTime(settlement.At.UtcDateTime) == snapshot.Today;
            month += settlement.Amount;
            if (settledToday)
            {
                day += settlement.Amount;
            }

            if (settlement.Class == budgetClass)
            {
                classMonth += settlement.Amount;
                if (settledToday)
                {
                    classDay += settlement.Amount;
                }
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
                    // Quelli che il registro non vedrà mai restano fino alla fine del loro mese.
                    _settled.RemoveAll(settlement => settlement.RecordedInCostLog
                        ? settlement.At < queriedAt
                        : !IsSameMonth(settlement.At, today));
                    Volatile.Write(ref _snapshot, fresh);
                }

                return fresh;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The LLM spend could not be read from the cost log");
                if (current is null || current.Today != today)
                {
                    return null;
                }

                // Si riusa la lettura di oggi, e la si considera fresca ancora per un poco: altrimenti
                // ogni chiamata ripeterebbe la query contro il database che non risponde.
                var retryAt = queriedAt - SpendSnapshotTtl + SpendRetryInterval;
                var reused = current with { QueriedAt = retryAt > current.QueriedAt ? retryAt : current.QueriedAt };
                Volatile.Write(ref _snapshot, reused);
                return reused;
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

    private sealed record Settlement(LlmBudgetClass Class, decimal Amount, DateTimeOffset At, bool RecordedInCostLog);

    private static bool IsSameMonth(DateTimeOffset at, DateOnly today) =>
        at.UtcDateTime.Year == today.Year && at.UtcDateTime.Month == today.Month;

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
