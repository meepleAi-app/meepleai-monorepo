using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Value object representing AI agent configuration for a game in user's library.
/// Encapsulates personality, model settings, and user preferences for the AI assistant.
/// </summary>
internal sealed class AgentConfiguration : ValueObject
{
    private const double MinTemperature = 0.0;
    private const double MaxTemperature = 2.0;
    private const int MinMaxTokens = 100;
    private const int MaxMaxTokens = 32000;

    /// <summary>
    /// LLM model identifier (e.g., "google/gemini-pro", "anthropic/claude-3-opus").
    /// </summary>
    public string LlmModel { get; }

    /// <summary>
    /// Temperature for response generation (0.0 = deterministic, 2.0 = creative).
    /// </summary>
    public double Temperature { get; }

    /// <summary>
    /// Maximum tokens for response generation.
    /// </summary>
    public int MaxTokens { get; }

    /// <summary>
    /// Personality style for the agent (e.g., "Amichevole", "Professionale", "Umoristico").
    /// </summary>
    public string Personality { get; }

    /// <summary>
    /// Detail level for responses (e.g., "Breve", "Normale", "Dettagliato").
    /// </summary>
    public string DetailLevel { get; }

    /// <summary>
    /// Optional personal notes about agent behavior preferences.
    /// </summary>
    public string? PersonalNotes { get; }

    /// <summary>
    /// Creates a new agent configuration with validation.
    /// </summary>
    private AgentConfiguration(
        string llmModel,
        double temperature,
        int maxTokens,
        string personality,
        string detailLevel,
        string? personalNotes)
    {
        if (string.IsNullOrWhiteSpace(llmModel))
            throw new ArgumentException("LLM model cannot be empty", nameof(llmModel));
        if (temperature < MinTemperature || temperature > MaxTemperature)
            throw new ArgumentOutOfRangeException(nameof(temperature), $"Temperature must be between {MinTemperature} and {MaxTemperature}");
        if (maxTokens < MinMaxTokens || maxTokens > MaxMaxTokens)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), $"MaxTokens must be between {MinMaxTokens} and {MaxMaxTokens}");
        if (string.IsNullOrWhiteSpace(personality))
            throw new ArgumentException("Personality cannot be empty", nameof(personality));
        if (string.IsNullOrWhiteSpace(detailLevel))
            throw new ArgumentException("DetailLevel cannot be empty", nameof(detailLevel));

        LlmModel = llmModel.Trim();
        Temperature = temperature;
        MaxTokens = maxTokens;
        Personality = personality.Trim();
        DetailLevel = detailLevel.Trim();
        PersonalNotes = personalNotes?.Trim();
    }

    /// <summary>
    /// Creates a new agent configuration from parameters.
    /// </summary>
    public static AgentConfiguration Create(
        string llmModel,
        double temperature,
        int maxTokens,
        string personality,
        string detailLevel,
        string? personalNotes = null)
    {
        return new AgentConfiguration(llmModel, temperature, maxTokens, personality, detailLevel, personalNotes);
    }

    /// <summary>
    /// Creates a default agent configuration with sensible defaults.
    /// </summary>
    public static AgentConfiguration CreateDefault()
    {
        return new AgentConfiguration(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale",
            personalNotes: null
        );
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LlmModel;
        yield return Temperature;
        yield return MaxTokens;
        yield return Personality;
        yield return DetailLevel;
        yield return PersonalNotes;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"Agent[Model={LlmModel}, Temp={Temperature}, Tokens={MaxTokens}, Personality={Personality}]";
}
