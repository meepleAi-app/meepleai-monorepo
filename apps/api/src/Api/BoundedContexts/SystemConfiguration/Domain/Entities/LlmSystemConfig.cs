namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Issue #5498: Runtime LLM system configuration stored in database.
/// appsettings.json provides defaults; DB values override when present.
/// Supports circuit breaker thresholds, budget limits, and fallback chain.
/// </summary>
/// <remarks>
/// ADR-095 D6, fetta 3 (#4166): i tetti giornaliero e mensile sono un limite che il gateway LLM
/// applica <b>prima</b> di ogni chiamata, per tutta l'AI. Dentro il tetto, il background e le
/// valutazioni hanno una quota ciascuno, così non consumano il budget delle risposte. Oltre la
/// soglia di degrado, sul consumo del giorno, il gateway passa al modello di riserva e mette in
/// pausa il background.
/// </remarks>
public sealed class LlmSystemConfig
{
    /// <summary>Tetto giornaliero finché l'admin non ne salva uno: il vincolo di ADR-095, sotto 20 € al mese.</summary>
    public const decimal DefaultDailyBudgetUsd = 1.00m;

    /// <summary>Tetto mensile finché l'admin non ne salva uno.</summary>
    public const decimal DefaultMonthlyBudgetUsd = 20.00m;

    /// <summary>Consumo del giorno, in percentuale del tetto, oltre il quale si degrada.</summary>
    public const int DefaultDegradeThresholdPercent = 80;

    /// <summary>Quota del background (traduzioni dei chunk, estrattore, eventi, admin) dentro il tetto.</summary>
    public const int DefaultBackgroundQuotaPercent = 30;

    /// <summary>Quota delle valutazioni (KbQuality, A/B) dentro il tetto.</summary>
    public const int DefaultEvaluationQuotaPercent = 20;

    public Guid Id { get; private set; }

    // Circuit breaker settings
    public int CircuitBreakerFailureThreshold { get; private set; }
    public int CircuitBreakerOpenDurationSeconds { get; private set; }
    public int CircuitBreakerSuccessThreshold { get; private set; }

    // Budget limits
    public decimal DailyBudgetUsd { get; private set; }
    public decimal MonthlyBudgetUsd { get; private set; }

    // Budget policy (#4166)
    public int DegradeThresholdPercent { get; private set; } = DefaultDegradeThresholdPercent;
    public int BackgroundQuotaPercent { get; private set; } = DefaultBackgroundQuotaPercent;
    public int EvaluationQuotaPercent { get; private set; } = DefaultEvaluationQuotaPercent;

    // Fallback chain (JSON array of provider names)
    public string FallbackChainJson { get; private set; } = "[]";

    // Audit
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private LlmSystemConfig() { } // EF Core

    private LlmSystemConfig(
        Guid id,
        int failureThreshold,
        int openDurationSeconds,
        int successThreshold,
        decimal dailyBudgetUsd,
        decimal monthlyBudgetUsd,
        string fallbackChainJson)
    {
        Id = id;
        CircuitBreakerFailureThreshold = failureThreshold;
        CircuitBreakerOpenDurationSeconds = openDurationSeconds;
        CircuitBreakerSuccessThreshold = successThreshold;
        DailyBudgetUsd = dailyBudgetUsd;
        MonthlyBudgetUsd = monthlyBudgetUsd;
        FallbackChainJson = fallbackChainJson;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Create a new LLM system config with default values.
    /// </summary>
    public static LlmSystemConfig CreateDefault()
    {
        return new LlmSystemConfig(
            Guid.NewGuid(),
            failureThreshold: 5,
            openDurationSeconds: 30,
            successThreshold: 3,
            dailyBudgetUsd: DefaultDailyBudgetUsd,
            monthlyBudgetUsd: DefaultMonthlyBudgetUsd,
            fallbackChainJson: "[\"Ollama\",\"OpenRouter\"]");
    }

    public void UpdateCircuitBreakerSettings(int failureThreshold, int openDurationSeconds, int successThreshold, Guid? updatedBy = null)
    {
        if (failureThreshold < 1) throw new ArgumentException("FailureThreshold must be >= 1", nameof(failureThreshold));
        if (openDurationSeconds < 1) throw new ArgumentException("OpenDurationSeconds must be >= 1", nameof(openDurationSeconds));
        if (successThreshold < 1) throw new ArgumentException("SuccessThreshold must be >= 1", nameof(successThreshold));

        CircuitBreakerFailureThreshold = failureThreshold;
        CircuitBreakerOpenDurationSeconds = openDurationSeconds;
        CircuitBreakerSuccessThreshold = successThreshold;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }

    public void UpdateBudgetLimits(decimal dailyBudgetUsd, decimal monthlyBudgetUsd, Guid? updatedBy = null)
    {
        if (dailyBudgetUsd < 0) throw new ArgumentException("DailyBudgetUsd cannot be negative", nameof(dailyBudgetUsd));
        if (monthlyBudgetUsd < 0) throw new ArgumentException("MonthlyBudgetUsd cannot be negative", nameof(monthlyBudgetUsd));
        if (dailyBudgetUsd > monthlyBudgetUsd) throw new ArgumentException("DailyBudgetUsd cannot exceed MonthlyBudgetUsd", nameof(dailyBudgetUsd));

        DailyBudgetUsd = dailyBudgetUsd;
        MonthlyBudgetUsd = monthlyBudgetUsd;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }

    /// <summary>
    /// #4166: soglia di degrado e quote del background e delle valutazioni, in percentuale del tetto.
    /// </summary>
    /// <remarks>
    /// Le due quote insieme non superano il tetto: la parte che resta è riservata alle risposte.
    /// </remarks>
    public void UpdateBudgetPolicy(
        int degradeThresholdPercent,
        int backgroundQuotaPercent,
        int evaluationQuotaPercent,
        Guid? updatedBy = null)
    {
        if (degradeThresholdPercent is < 1 or > 100)
            throw new ArgumentException("DegradeThresholdPercent must be between 1 and 100", nameof(degradeThresholdPercent));
        if (backgroundQuotaPercent is < 0 or > 100)
            throw new ArgumentException("BackgroundQuotaPercent must be between 0 and 100", nameof(backgroundQuotaPercent));
        if (evaluationQuotaPercent is < 0 or > 100)
            throw new ArgumentException("EvaluationQuotaPercent must be between 0 and 100", nameof(evaluationQuotaPercent));
        if (backgroundQuotaPercent + evaluationQuotaPercent > 100)
            throw new ArgumentException("BackgroundQuotaPercent and EvaluationQuotaPercent together cannot exceed 100", nameof(evaluationQuotaPercent));

        DegradeThresholdPercent = degradeThresholdPercent;
        BackgroundQuotaPercent = backgroundQuotaPercent;
        EvaluationQuotaPercent = evaluationQuotaPercent;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }

    public void UpdateFallbackChain(string fallbackChainJson, Guid? updatedBy = null)
    {
        FallbackChainJson = fallbackChainJson ?? throw new ArgumentNullException(nameof(fallbackChainJson));
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }
}
