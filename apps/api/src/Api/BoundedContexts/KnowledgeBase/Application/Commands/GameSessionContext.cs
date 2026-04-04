namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Contesto della sessione di gioco passato all'agente AI per rispondere
/// in modo contestuale al gioco in corso, ai giocatori e alla lingua.
/// </summary>
internal record GameSessionContext
{
    public Guid GameId { get; init; }
    public string GameTitle { get; init; }
    public IReadOnlyList<string> Players { get; init; }
    public int CurrentTurn { get; init; }
    public string ResponseLanguage { get; init; }

    private GameSessionContext(
        Guid gameId,
        string gameTitle,
        IReadOnlyList<string> players,
        int currentTurn,
        string responseLanguage)
    {
        GameId = gameId;
        GameTitle = gameTitle;
        Players = players;
        CurrentTurn = currentTurn;
        ResponseLanguage = responseLanguage;
    }

    /// <summary>
    /// Factory method that validates and creates a <see cref="GameSessionContext"/>.
    /// </summary>
    public static GameSessionContext Create(
        Guid gameId,
        string gameTitle,
        IReadOnlyList<string> players,
        int currentTurn,
        string responseLanguage = "it")
    {
        if (string.IsNullOrWhiteSpace(gameTitle))
            throw new ArgumentException("GameTitle e' obbligatorio.", nameof(gameTitle));
        if (players == null || players.Count == 0)
            throw new ArgumentException("Almeno un giocatore e' richiesto.", nameof(players));

        return new GameSessionContext(gameId, gameTitle, players, currentTurn, responseLanguage);
    }

    public string ToSystemPromptEnrichment()
    {
        var playerList = string.Join(", ", Players);
        var languageLabel = string.Equals(ResponseLanguage, "it", StringComparison.Ordinal) ? "italiano" : ResponseLanguage;
        return
            $"CONTESTO PARTITA CORRENTE:\n" +
            $"- Gioco: {GameTitle}\n" +
            $"- Giocatori: {playerList}\n" +
            $"- Turno corrente: {CurrentTurn}\n" +
            $"- Lingua di risposta: {languageLabel}\n\n" +
            $"ISTRUZIONI:\n" +
            $"- Rispondi SEMPRE in italiano, anche se il manuale del gioco e' in inglese.\n" +
            $"- Contestualizza le risposte al gioco \"{GameTitle}\" in corso.\n" +
            $"- Quando citi regole, riferisciti ai giocatori per nome ({playerList}).\n" +
            $"- Mantieni le risposte concise (max 150 parole) e leggibili ad alta voce.\n" +
            $"- Se non hai il manuale di {GameTitle}, dillo chiaramente e offri di aiutare\n" +
            $"  con regole generali o suggerisci di caricare il PDF del manuale.";
    }
}
