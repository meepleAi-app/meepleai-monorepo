using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Contenuto di una versione di <see cref="Entities.AgentProfile"/> (ADR-095 D1): identità, modello,
/// generazione, prompt per lingua, recupero e fonti. Immutabile: cambiare il comportamento
/// dell'agente significa creare una nuova versione, non modificare questa.
/// </summary>
public sealed class AgentProfileContent : ValueObject
{
    public const int MaxResponseTokensLimit = 32000;

    public string Name { get; }
    public string Description { get; }
    public string Persona { get; }
    public string PrimaryModelId { get; }
    public string? FallbackModelId { get; }
    public decimal Temperature { get; }
    public int MaxResponseTokens { get; }
    public CitationStyle CitationStyle { get; }
    public IReadOnlyDictionary<string, string> SystemPrompts { get; }
    public string FallbackLanguage { get; }
    public int CandidatePoolSize { get; }
    public int FinalTopK { get; }
    public decimal MinScore { get; }
    public decimal VectorWeight { get; }
    public decimal KeywordWeight { get; }
    public bool RerankerEnabled { get; }
    public IReadOnlyList<DocumentCategory> AllowedCategories { get; }

    private AgentProfileContent(
        string name,
        string description,
        string persona,
        string primaryModelId,
        string? fallbackModelId,
        decimal temperature,
        int maxResponseTokens,
        CitationStyle citationStyle,
        IReadOnlyDictionary<string, string> systemPrompts,
        string fallbackLanguage,
        int candidatePoolSize,
        int finalTopK,
        decimal minScore,
        decimal vectorWeight,
        decimal keywordWeight,
        bool rerankerEnabled,
        IReadOnlyList<DocumentCategory> allowedCategories)
    {
        Name = name;
        Description = description;
        Persona = persona;
        PrimaryModelId = primaryModelId;
        FallbackModelId = fallbackModelId;
        Temperature = temperature;
        MaxResponseTokens = maxResponseTokens;
        CitationStyle = citationStyle;
        SystemPrompts = systemPrompts;
        FallbackLanguage = fallbackLanguage;
        CandidatePoolSize = candidatePoolSize;
        FinalTopK = finalTopK;
        MinScore = minScore;
        VectorWeight = vectorWeight;
        KeywordWeight = keywordWeight;
        RerankerEnabled = rerankerEnabled;
        AllowedCategories = allowedCategories;
    }

    public static AgentProfileContent Create(
        string name,
        string description,
        string persona,
        string primaryModelId,
        string? fallbackModelId,
        decimal temperature,
        int maxResponseTokens,
        CitationStyle citationStyle,
        IReadOnlyDictionary<string, string> systemPrompts,
        string fallbackLanguage,
        int candidatePoolSize,
        int finalTopK,
        decimal minScore,
        decimal vectorWeight,
        decimal keywordWeight,
        bool rerankerEnabled,
        IReadOnlyCollection<DocumentCategory> allowedCategories)
    {
        Require(!string.IsNullOrWhiteSpace(name), nameof(name), "Name is required.");
        Require(!string.IsNullOrWhiteSpace(primaryModelId), nameof(primaryModelId), "Primary model is required.");
        Require(fallbackModelId is null || !string.IsNullOrWhiteSpace(fallbackModelId),
            nameof(fallbackModelId), "Fallback model must be null or non-empty.");
        Require(Enum.IsDefined(citationStyle), nameof(citationStyle), "Unknown citation style.");
        Require(temperature is >= 0m and <= 2m, nameof(temperature), "Temperature must be between 0 and 2.");
        Require(maxResponseTokens is >= 1 and <= MaxResponseTokensLimit, nameof(maxResponseTokens),
            $"Max response tokens must be between 1 and {MaxResponseTokensLimit}.");

        Require(!string.IsNullOrWhiteSpace(fallbackLanguage), nameof(fallbackLanguage), "Fallback language is required.");
        var language = NormalizeLanguage(fallbackLanguage);
        Require(systemPrompts is { Count: > 0 }, nameof(systemPrompts), "At least one system prompt is required.");
        var prompts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (lang, prompt) in systemPrompts)
        {
            Require(!string.IsNullOrWhiteSpace(lang), nameof(systemPrompts), "Prompt language is required.");
            Require(!string.IsNullOrWhiteSpace(prompt), nameof(systemPrompts), $"Prompt for '{lang}' is empty.");
            Require(prompts.TryAdd(NormalizeLanguage(lang), prompt), nameof(systemPrompts),
                $"Prompt for '{lang}' is declared twice.");
        }
        Require(prompts.ContainsKey(language), nameof(systemPrompts),
            $"A system prompt for the fallback language '{language}' is required.");

        Require(candidatePoolSize >= 1, nameof(candidatePoolSize), "Candidate pool size must be positive.");
        Require(finalTopK >= 1 && finalTopK <= candidatePoolSize, nameof(finalTopK),
            "Final top-K must be between 1 and the candidate pool size.");
        Require(minScore is >= 0m and <= 1m, nameof(minScore), "Min score must be between 0 and 1.");
        Require(vectorWeight >= 0m && keywordWeight >= 0m && vectorWeight + keywordWeight > 0m,
            nameof(vectorWeight), "Fusion weights must be non-negative and not both zero.");

        Require(allowedCategories is { Count: > 0 }, nameof(allowedCategories), "At least one document category is required.");
        Require(allowedCategories.All(c => Enum.IsDefined(c)), nameof(allowedCategories), "Unknown document category.");

        return new AgentProfileContent(
            name.Trim(),
            description?.Trim() ?? string.Empty,
            persona?.Trim() ?? string.Empty,
            primaryModelId.Trim(),
            fallbackModelId?.Trim(),
            temperature,
            maxResponseTokens,
            citationStyle,
            prompts.AsReadOnly(),
            language,
            candidatePoolSize,
            finalTopK,
            minScore,
            vectorWeight,
            keywordWeight,
            rerankerEnabled,
            allowedCategories.Distinct().Order().ToArray());
    }

    /// <summary>
    /// Ricostruisce un contenuto già persistito <b>senza rivalidarlo</b>. Le versioni salvate sono
    /// storia immutabile: se le regole di <see cref="Create"/> si stringono, una versione archiviata
    /// sotto le regole di allora deve continuare a caricarsi, altrimenti non si carica più l'aggregato.
    /// Riservato alla persistenza; ogni contenuto nuovo passa da <see cref="Create"/>.
    /// </summary>
    internal static AgentProfileContent Restore(
        string name,
        string description,
        string persona,
        string primaryModelId,
        string? fallbackModelId,
        decimal temperature,
        int maxResponseTokens,
        CitationStyle citationStyle,
        IReadOnlyDictionary<string, string> systemPrompts,
        string fallbackLanguage,
        int candidatePoolSize,
        int finalTopK,
        decimal minScore,
        decimal vectorWeight,
        decimal keywordWeight,
        bool rerankerEnabled,
        IReadOnlyCollection<DocumentCategory> allowedCategories) =>
        new(
            name,
            description,
            persona,
            primaryModelId,
            fallbackModelId,
            temperature,
            maxResponseTokens,
            citationStyle,
            new SortedDictionary<string, string>(systemPrompts.ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal).AsReadOnly(),
            fallbackLanguage,
            candidatePoolSize,
            finalTopK,
            minScore,
            vectorWeight,
            keywordWeight,
            rerankerEnabled,
            // Normalizzare non è validare: le categorie sono un insieme, e l'uguaglianza le confronta
            // in ordine, quindi un jsonb scritto in un altro ordine deve tornare uguale a Create.
            allowedCategories.Distinct().Order().ToArray());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Description;
        yield return Persona;
        yield return PrimaryModelId;
        yield return FallbackModelId;
        yield return Temperature;
        yield return MaxResponseTokens;
        yield return CitationStyle;
        // I conteggi tengono allineate le sequenze: senza, prompt e categorie di lunghezza diversa
        // sposterebbero i campi successivi e due contenuti diversi potrebbero risultare uguali.
        yield return SystemPrompts.Count;
        foreach (var (lang, prompt) in SystemPrompts)
        {
            yield return lang;
            yield return prompt;
        }
        yield return FallbackLanguage;
        yield return CandidatePoolSize;
        yield return FinalTopK;
        yield return MinScore;
        yield return VectorWeight;
        yield return KeywordWeight;
        yield return RerankerEnabled;
        yield return AllowedCategories.Count;
        foreach (var category in AllowedCategories)
        {
            yield return category;
        }
    }

    private static string NormalizeLanguage(string language) => language.Trim().ToLowerInvariant();

    private static void Require(bool condition, string propertyName, string message)
    {
        if (!condition)
        {
            throw new ValidationException(propertyName, message);
        }
    }
}
