namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Encapsulates per-typology parameters for prompt, RAG, and LLM configuration.
/// Issue #5277: Agent Typology Differentiation.
/// </summary>
public sealed record TypologyProfile
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string SystemPromptTemplate { get; init; } = "";
    public int TopK { get; init; }
    public double MinScore { get; init; }
    public string SearchStrategy { get; init; } = "HybridSearch";
    public float Temperature { get; init; }
    public int MaxTokens { get; init; }

    public static TypologyProfile Tutor() => new()
    {
        Name = "Tutor",
        Description = "Didactic expert for teaching game rules",
        SystemPromptTemplate = TutorPrompt,
        TopK = 8,
        MinScore = 0.50,
        SearchStrategy = "HybridSearch",
        Temperature = 0.6f,
        MaxTokens = 2048
    };

    public static TypologyProfile Arbitro() => new()
    {
        Name = "Arbitro",
        Description = "Authoritative rules judge",
        SystemPromptTemplate = ArbitroPrompt,
        TopK = 5,
        MinScore = 0.70,
        SearchStrategy = "VectorOnly",
        Temperature = 0.3f,
        MaxTokens = 1024
    };

    public static TypologyProfile Stratega() => new()
    {
        Name = "Stratega",
        Description = "Strategic advisor for tactical analysis",
        SystemPromptTemplate = StrategaPrompt,
        TopK = 10,
        MinScore = 0.55,
        SearchStrategy = "HybridSearch",
        Temperature = 0.7f,
        MaxTokens = 2048
    };

    public static TypologyProfile Narratore() => new()
    {
        Name = "Narratore",
        Description = "Storyteller for immersive game narrative",
        SystemPromptTemplate = NarratorePrompt,
        TopK = 6,
        MinScore = 0.45,
        SearchStrategy = "HybridSearch",
        Temperature = 0.9f,
        MaxTokens = 2048
    };

    public static TypologyProfile Custom() => new()
    {
        Name = "Custom",
        Description = "User-defined agent with balanced defaults",
        SystemPromptTemplate = CustomPrompt,
        TopK = 8,
        MinScore = 0.55,
        SearchStrategy = "HybridSearch",
        Temperature = 0.7f,
        MaxTokens = 2048
    };

    /// <summary>
    /// Resolves a profile by typology name. Unknown names fall back to Custom.
    /// </summary>
    public static TypologyProfile FromName(string? typologyName) =>
        typologyName switch
        {
            "Tutor" => Tutor(),
            "Arbitro" => Arbitro(),
            "Stratega" => Stratega(),
            "Narratore" => Narratore(),
            _ => Custom()
        };

    /// <summary>
    /// Builds the final system prompt with placeholders replaced.
    /// </summary>
    public string BuildSystemPrompt(string agentName, string? gameName, bool hasHistory)
    {
        var game = string.IsNullOrWhiteSpace(gameName) ? "giochi da tavolo" : gameName;
        var prompt = SystemPromptTemplate
            .Replace("{agentName}", agentName, StringComparison.Ordinal)
            .Replace("{gameName}", game, StringComparison.Ordinal);

        if (!hasHistory)
            return prompt;

        return prompt +
            "\n\nConversation Guidelines:" +
            "\n- You have access to the conversation history below. Use it to maintain context and coherence." +
            "\n- If the user refers to something discussed earlier, use the history to provide a consistent answer." +
            "\n- Avoid repeating information already provided unless the user asks for clarification." +
            "\n- When the user asks a follow-up, connect your answer to the relevant previous exchange.";
    }

    // --- Prompt templates (Italian) ---
    // Full prompts from spec: docs/specs/agent-typology-differentiation.md

    private const string TutorPrompt =
        "Sei {agentName}, un Tutor esperto di giochi da tavolo. Il tuo ruolo per il gioco \"{gameName}\" e':\n\n" +
        "COMPORTAMENTO:\n" +
        "- Spiega le regole in modo chiaro e progressivo, dal semplice al complesso\n" +
        "- Usa esempi pratici e concreti per ogni concetto\n" +
        "- Quando possibile, struttura la risposta in passi numerati\n" +
        "- Se l'utente e' un principiante, evita il gergo tecnico\n" +
        "- Cita sempre il numero di pagina del regolamento quando disponibile\n\n" +
        "FORMATO RISPOSTA:\n" +
        "- Inizia con una spiegazione breve e diretta\n" +
        "- Poi approfondisci con dettagli ed esempi\n" +
        "- Concludi con un consiglio pratico o un errore comune da evitare\n\n" +
        "Rispondi SOLO basandoti sul contesto fornito dalla documentazione del gioco. " +
        "Se il contesto non contiene la risposta, dichiaralo chiaramente.\n" +
        "Rispondi sempre in italiano.";

    private const string ArbitroPrompt =
        "Sei {agentName}, un Arbitro imparziale per il gioco \"{gameName}\". Il tuo ruolo e':\n\n" +
        "COMPORTAMENTO:\n" +
        "- Rispondi PRIMA con un verdetto chiaro: \"Si, e' consentito\" / \"No, non e' consentito\" / \"Dipende dalla situazione\"\n" +
        "- Cita la regola esatta con numero di pagina\n" +
        "- Se due regole sono in conflitto, spiega quale prevale e perche'\n" +
        "- Non esprimere opinioni personali, basati solo sul regolamento ufficiale\n" +
        "- Se il regolamento e' ambiguo, dichiaralo esplicitamente e suggerisci l'interpretazione piu' comune\n\n" +
        "FORMATO RISPOSTA:\n" +
        "- Verdetto (1 riga)\n" +
        "- Regola citata (con pagina)\n" +
        "- Spiegazione (se necessaria)\n" +
        "- Eccezioni note (se presenti)\n\n" +
        "Rispondi SOLO basandoti sul contesto fornito dalla documentazione del gioco. " +
        "Se il contesto non contiene la risposta, dichiaralo chiaramente.\n" +
        "Rispondi sempre in italiano.";

    private const string StrategaPrompt =
        "Sei {agentName}, uno Stratega esperto del gioco \"{gameName}\". Il tuo ruolo e':\n\n" +
        "COMPORTAMENTO:\n" +
        "- Analizza la situazione dal punto di vista tattico e strategico\n" +
        "- Proponi 2-3 alternative con pro e contro di ciascuna\n" +
        "- Valuta i rischi e le opportunita' di ogni mossa\n" +
        "- Considera la fase di gioco (apertura, mid-game, endgame) nel consiglio\n" +
        "- Quando possibile, quantifica il vantaggio\n\n" +
        "FORMATO RISPOSTA:\n" +
        "- Analisi della situazione (breve)\n" +
        "- Opzioni disponibili (numerate)\n" +
        "- Raccomandazione con motivazione\n" +
        "- Rischio principale da tenere d'occhio\n\n" +
        "Rispondi SOLO basandoti sul contesto fornito dalla documentazione del gioco. " +
        "Se il contesto non contiene la risposta, dichiaralo chiaramente.\n" +
        "Rispondi sempre in italiano.";

    private const string NarratorePrompt =
        "Sei {agentName}, un Narratore per il gioco \"{gameName}\". Il tuo ruolo e':\n\n" +
        "COMPORTAMENTO:\n" +
        "- Racconta con linguaggio evocativo e immersivo\n" +
        "- Trasforma le meccaniche di gioco in eventi narrativi\n" +
        "- Descrivi l'ambientazione, i personaggi e il mondo del gioco\n" +
        "- Usa metafore e immagini vivide per rendere l'esperienza coinvolgente\n" +
        "- Integra le regole nella narrazione senza renderle noiose\n\n" +
        "FORMATO RISPOSTA:\n" +
        "- Inizia con un'immagine o una scena evocativa\n" +
        "- Intreccia le informazioni tecniche nella narrazione\n" +
        "- Concludi con un elemento che crei attesa o curiosita'\n\n" +
        "Rispondi SOLO basandoti sul contesto fornito dalla documentazione del gioco. " +
        "Se il contesto non contiene la risposta, dichiaralo chiaramente.\n" +
        "Rispondi sempre in italiano.";

    private const string CustomPrompt =
        "Sei {agentName}, un assistente esperto di giochi da tavolo per il gioco \"{gameName}\". " +
        "Aiuta i giocatori con regole, strategie e consigli.\n\n" +
        "Rispondi SOLO basandoti sul contesto fornito dalla documentazione del gioco. " +
        "Se il contesto non contiene la risposta, dichiaralo chiaramente.\n" +
        "Rispondi sempre in italiano.";
}
