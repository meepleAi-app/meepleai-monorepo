namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Default configuration values for new agent instances.
/// Centralizes defaults to avoid hardcoded magic strings across handlers.
/// </summary>
internal static class AgentDefaults
{
    /// <summary>
    /// Default OpenRouter model. Reads from OPENROUTER_DEFAULT_MODEL env var at startup.
    /// Falls back to the paid Llama 3.3 70B model (not the :free variant which is rate-limited).
    /// </summary>
    public static readonly string DefaultModel =
        Environment.GetEnvironmentVariable("OPENROUTER_DEFAULT_MODEL")
        ?? "meta-llama/llama-3.3-70b-instruct";

    /// <summary>
    /// Default LLM provider: 0 = OpenRouter, 1 = Ollama.
    /// </summary>
    public const int DefaultLlmProvider = 0;

    /// <summary>
    /// Default temperature for agent responses.
    /// </summary>
    public const decimal DefaultTemperature = 0.3m;

    /// <summary>
    /// Default max tokens for agent responses.
    /// </summary>
    public const int DefaultMaxTokens = 2048;

    /// <summary>
    /// Nome della variabile d'ambiente che decide quale modello di chat usa Ollama.
    /// </summary>
    /// <remarks>
    /// È la <b>stessa</b> variabile che <c>infra/docker-compose.yml</c> passa a <c>ollama-pull</c>
    /// per decidere quale modello scaricare. Finché l'API non la leggeva, il knob documentato in
    /// quel file («<c>OLLAMA_CHAT_MODEL=qwen2.5:1.5b make dev</c> scarica ~1GB invece di 4,7GB»)
    /// era inerte di qua: compose scaricava il modello richiesto e l'API ne chiedeva un altro,
    /// quindi una chat instradata su Ollama rispondeva <c>404 model not found</c>. Un dialetto,
    /// non una manopola.
    /// </remarks>
    public const string OllamaChatModelEnvVar = "OLLAMA_CHAT_MODEL";

    /// <summary>
    /// Modello di chat Ollama usato quando <see cref="OllamaChatModelEnvVar"/> non è impostata.
    /// Deve restare allineato al default dell'espressione compose
    /// <c>${OLLAMA_CHAT_MODEL:-llama3:8b}</c>.
    /// </summary>
    public const string OllamaChatModelDefault = "llama3:8b";

    /// <summary>
    /// Local Ollama fallback model (zero cost).
    /// Used when free-tier OpenRouter models are rate-limited or unavailable.
    /// </summary>
    /// <remarks>
    /// Property e non <c>const</c>: la risoluzione deve avvenire a runtime, altrimenti
    /// <see cref="OllamaChatModelEnvVar"/> non avrebbe effetto. Rispecchia
    /// <see cref="DefaultModel"/>, che legge già <c>OPENROUTER_DEFAULT_MODEL</c>; l'asimmetria fra
    /// le due era il difetto.
    /// </remarks>
    public static string OllamaFallbackModel =>
        ResolveOllamaChatModel(Environment.GetEnvironmentVariable(OllamaChatModelEnvVar));

    /// <summary>
    /// Risolve il modello di chat Ollama da un valore configurato, che può essere assente o vuoto.
    /// </summary>
    /// <remarks>
    /// Funzione pura, separata dalla lettura dell'ambiente, perché un test possa esercitare la
    /// regola — assente, vuoto, con spazi intorno — senza mutare una variabile globale al processo
    /// mentre il resto della suite gira in parallelo.
    /// </remarks>
    internal static string ResolveOllamaChatModel(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? OllamaChatModelDefault : configured.Trim();
}
