using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// La sezione «Assistente in partita» di una versione del profilo (spec 2026-10-11 §3.2): se l'assistente
/// è abilitato, con quali prompt e con quali limiti gira un suo turno. Il modello è quello del profilo; la
/// riserva del profilo si usa solo se <see cref="UseReserveModel"/> lo dice.
/// <para>
/// I valori di default sono quelli misurati dallo spike (§7.1). Una versione senza questa sezione vale
/// <see cref="Disabled"/>.
/// </para>
/// </summary>
public sealed class SessionAssistantSettings : ValueObject
{
    public const decimal DefaultTemperature = 0.2m;
    public const int DefaultMaxResponseTokens = 600;
    public const int DefaultMaxToolIterations = 3;
    public const int DefaultTurnTimeoutSeconds = 15;
    public const int MaxToolIterationsLimit = 8;
    public const int MinTurnTimeoutSeconds = 5;
    public const int MaxTurnTimeoutSeconds = 120;

    private static readonly IReadOnlyDictionary<string, string> NoPrompts =
        new SortedDictionary<string, string>(StringComparer.Ordinal).AsReadOnly();

    /// <summary>L'assistente spento: è ciò che vale una versione del profilo senza la sezione.</summary>
    public static SessionAssistantSettings Disabled { get; } = new(
        enabled: false, NoPrompts, DefaultTemperature, DefaultMaxResponseTokens, DefaultMaxToolIterations,
        DefaultTurnTimeoutSeconds, useReserveModel: false);

    public bool Enabled { get; }
    public IReadOnlyDictionary<string, string> SystemPrompts { get; }
    public decimal Temperature { get; }
    public int MaxResponseTokens { get; }

    /// <summary>I giri con strumenti di un turno; dopo, una chiamata finale senza strumenti (ADR-097).</summary>
    public int MaxToolIterations { get; }

    public int TurnTimeoutSeconds { get; }

    /// <summary>Se un turno può scendere alla riserva del profilo. Spento finché la riserva non supera il gate (§7.1).</summary>
    public bool UseReserveModel { get; }

    private SessionAssistantSettings(
        bool enabled,
        IReadOnlyDictionary<string, string> systemPrompts,
        decimal temperature,
        int maxResponseTokens,
        int maxToolIterations,
        int turnTimeoutSeconds,
        bool useReserveModel)
    {
        Enabled = enabled;
        SystemPrompts = systemPrompts;
        Temperature = temperature;
        MaxResponseTokens = maxResponseTokens;
        MaxToolIterations = maxToolIterations;
        TurnTimeoutSeconds = turnTimeoutSeconds;
        UseReserveModel = useReserveModel;
    }

    public static SessionAssistantSettings Create(
        bool enabled,
        IReadOnlyDictionary<string, string> systemPrompts,
        decimal temperature = DefaultTemperature,
        int maxResponseTokens = DefaultMaxResponseTokens,
        int maxToolIterations = DefaultMaxToolIterations,
        int turnTimeoutSeconds = DefaultTurnTimeoutSeconds,
        bool useReserveModel = false)
    {
        ArgumentNullException.ThrowIfNull(systemPrompts);

        var prompts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (lang, prompt) in systemPrompts)
        {
            Require(!string.IsNullOrWhiteSpace(lang), nameof(systemPrompts), "Prompt language is required.");
            Require(!string.IsNullOrWhiteSpace(prompt), nameof(systemPrompts), $"Prompt for '{lang}' is empty.");
            Require(prompts.TryAdd(Normalize(lang), prompt), nameof(systemPrompts), $"Prompt for '{lang}' is declared twice.");
        }

        Require(!enabled || prompts.Count > 0, nameof(systemPrompts),
            "An enabled session assistant needs at least one system prompt.");
        Require(temperature is >= 0m and <= 2m, nameof(temperature), "Temperature must be between 0 and 2.");
        Require(maxResponseTokens is >= 1 and <= AgentProfileContent.MaxResponseTokensLimit, nameof(maxResponseTokens),
            $"Max response tokens must be between 1 and {AgentProfileContent.MaxResponseTokensLimit}.");
        Require(maxToolIterations is >= 1 and <= MaxToolIterationsLimit, nameof(maxToolIterations),
            $"Max tool iterations must be between 1 and {MaxToolIterationsLimit}.");
        Require(turnTimeoutSeconds is >= MinTurnTimeoutSeconds and <= MaxTurnTimeoutSeconds, nameof(turnTimeoutSeconds),
            $"Turn timeout must be between {MinTurnTimeoutSeconds} and {MaxTurnTimeoutSeconds} seconds.");

        return new SessionAssistantSettings(
            enabled, prompts.AsReadOnly(), temperature, maxResponseTokens, maxToolIterations, turnTimeoutSeconds, useReserveModel);
    }

    /// <summary>
    /// Ricostruisce una sezione già persistita <b>senza rivalidarla</b>, come
    /// <see cref="AgentProfileContent.Restore"/>. Un campo assente vale il default misurato.
    /// </summary>
    internal static SessionAssistantSettings Restore(
        bool enabled,
        IReadOnlyDictionary<string, string>? systemPrompts,
        decimal? temperature,
        int? maxResponseTokens,
        int? maxToolIterations,
        int? turnTimeoutSeconds,
        bool useReserveModel) =>
        new(
            enabled,
            systemPrompts is null
                ? NoPrompts
                : new SortedDictionary<string, string>(systemPrompts.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal).AsReadOnly(),
            temperature ?? DefaultTemperature,
            maxResponseTokens ?? DefaultMaxResponseTokens,
            maxToolIterations ?? DefaultMaxToolIterations,
            turnTimeoutSeconds ?? DefaultTurnTimeoutSeconds,
            useReserveModel);

    /// <summary>
    /// Il prompt per una lingua: quello esatto, o quello della lingua senza la regione («en-US» → «en»).
    /// <c>null</c> se non c'è: chi chiama ricade sulla lingua di riserva del profilo.
    /// </summary>
    public string? PromptFor(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var key = Normalize(language);
        if (SystemPrompts.TryGetValue(key, out var exact))
        {
            return exact;
        }

        var separator = key.IndexOfAny(['-', '_']);
        return separator > 0 && SystemPrompts.TryGetValue(key[..separator], out var primary) ? primary : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Enabled;
        // Il conteggio tiene allineate le sequenze, come in AgentProfileContent.
        yield return SystemPrompts.Count;
        foreach (var (lang, prompt) in SystemPrompts)
        {
            yield return lang;
            yield return prompt;
        }

        yield return Temperature;
        yield return MaxResponseTokens;
        yield return MaxToolIterations;
        yield return TurnTimeoutSeconds;
        yield return UseReserveModel;
    }

    private static string Normalize(string language) => language.Trim().ToLowerInvariant();

    private static void Require(bool condition, string propertyName, string message)
    {
        if (!condition)
        {
            throw new ValidationException(propertyName, message);
        }
    }
}
