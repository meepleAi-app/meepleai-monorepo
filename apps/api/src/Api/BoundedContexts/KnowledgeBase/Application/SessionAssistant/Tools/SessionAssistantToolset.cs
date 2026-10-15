using System.ComponentModel;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

/// <summary>
/// Gli strumenti dell'assistente in partita (spec §5.1). Leggono solo da <see cref="ISessionAssistantReader"/>
/// e propongono solo attraverso <see cref="IAssistantProposalSink"/>: il modello non scrive mai la partita.
/// Un'istanza per turno.
/// </summary>
internal sealed class SessionAssistantToolset
{
    public const string StatoPartita = "stato_partita";
    public const string CercaRegole = "cerca_regole";
    public const string ProponiPunteggio = "proponi_punteggio";
    public const string ProponiTurno = "proponi_turno";

    private const string PendingNote = "Proposta in attesa di conferma dell'host: NON è ancora registrata.";

    private const string FoundNote =
        "Rispondi solo con ciò che dicono questi passaggi, con parole tue e citando la pagina: non copiarli parola per parola.";

    private const string NothingFoundNote =
        "Il regolamento consultabile non ne parla: dillo all'host e non rispondere a memoria.";

    /// <summary>Risultati compatti: il JSON indentato di default gonfia i token di ogni giro.</summary>
    private static readonly JsonSerializerOptions ToolJson = CreateToolJson();

    private readonly ISessionAssistantReader _reader;
    private readonly IAssistantProposalSink _sink;
    private readonly IAssistantPlayerMatcher _matcher;
    private readonly IAssistantRuleSource? _rules;
    private readonly List<ChunkCitation> _citations = [];
    private int _errorCount;

    /// <remarks>
    /// <c>rules</c> è il regolamento del gioco della sessione (#4411). Null per una sessione senza gioco: allora
    /// cerca_regole non viene offerto (spec §4.2).
    /// </remarks>
    public SessionAssistantToolset(
        ISessionAssistantReader reader, IAssistantProposalSink sink, IAssistantPlayerMatcher matcher, IAssistantRuleSource? rules = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _rules = rules;
    }

    /// <summary>Quante volte uno strumento ha risposto con un errore in questo turno.</summary>
    public int ErrorCount => _errorCount;

    /// <summary>Le citazioni di tutte le chiamate a cerca_regole di questo turno, una per passaggio.</summary>
    public IReadOnlyList<ChunkCitation> Citations
    {
        get
        {
            lock (_citations)
            {
                return [.. _citations];
            }
        }
    }

    /// <summary>cerca_regole c'è solo se il turno ha una sorgente del regolamento: una sessione senza gioco non ne ha (§4.2).</summary>
    public IList<AITool> CreateTools()
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetStateAsync, StatoPartita,
                "Legge lo stato attuale della partita: giocatori e totali, giocatore di turno, round, fase, dimensioni del punteggio e punteggi per round. Chiamalo prima di rispondere su punteggi o turni.",
                ToolJson),
        };
        if (_rules is not null)
        {
            tools.Add(AIFunctionFactory.Create(SearchRulesAsync, CercaRegole,
                "Cerca nel regolamento del gioco i passaggi utili a una domanda sulle regole. Rispondi solo con ciò che trovi, citando la pagina, con parole tue.",
                ToolJson));
        }

        tools.Add(AIFunctionFactory.Create(ProposeScoreAsync, ProponiPunteggio,
            "Propone di aggiungere dei punti a un giocatore. NON registra: crea una proposta che l'host conferma.",
            ToolJson));
        tools.Add(AIFunctionFactory.Create(ProposeTurnAsync, ProponiTurno,
            "Propone di passare il turno al giocatore successivo. NON registra: crea una proposta che l'host conferma.",
            ToolJson));
        return tools;
    }

    /// <summary>stato_partita. I nomi arrivano al modello su una riga sola, e corti (#4393).</summary>
    public async Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await _reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        return state with { Players = state.Players.Select(p => p with { Name = AssistantNames.ForModel(p.Name) }).ToList() };
    }

    public async Task<object> SearchRulesAsync(
        [Description("La domanda sulle regole, con parole proprie")] string domanda,
        CancellationToken cancellationToken = default)
    {
        if (_rules is null)
        {
            return Error("regolamento_non_disponibile", "Questa partita non ha un regolamento da consultare: non rispondere a memoria.");
        }

        if (string.IsNullOrWhiteSpace(domanda))
        {
            return Error("domanda_mancante", "Serve la domanda sulle regole.");
        }

        var search = await _rules.SearchAsync(domanda.Trim(), cancellationToken).ConfigureAwait(false);
        if (!search.Accessible)
        {
            return Error(
                "regolamento_non_accessibile",
                "L'host non ha accesso al regolamento di questo gioco: si ottiene dichiarando di possederlo. Non rispondere a memoria.");
        }

        lock (_citations)
        {
            foreach (var citation in search.Citations)
            {
                if (!_citations.Exists(c => SamePassage(c, citation)))
                {
                    _citations.Add(citation);
                }
            }
        }

        return new RulePassagesFound(search.Passages, search.Passages.Count == 0 ? NothingFoundNote : FoundNote);
    }

    public async Task<object> ProposeScoreAsync(
        [Description("Il nome del giocatore, come l'ha detto l'host")] string giocatore,
        [Description("I punti guadagnati adesso: un incremento, anche negativo")] int punti,
        [Description("La dimensione del punteggio; omettila se la partita ne ha una sola")] string? dimensione = null,
        [Description("Il round; omettilo per il round corrente")] int? round = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(giocatore))
        {
            return Error("giocatore_mancante", "Serve il nome del giocatore: chiedilo all'host.");
        }

        // Su long: Math.Abs(int.MinValue) lancerebbe. Il limite è quello dell'aggregato, che resta l'ultima
        // difesa: qui il modello riceve un errore che può leggere e riportare all'host.
        if (Math.Abs((long)punti) > AssistantProposal.MaxPointsPerProposal)
        {
            return Error(
                "punti_fuori_limite",
                $"I punti devono stare fra -{AssistantProposal.MaxPointsPerProposal} e {AssistantProposal.MaxPointsPerProposal}: chiedi all'host di ripetere il numero.");
        }

        var state = await _reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.CanRecordScores)
        {
            return Error("sessione_non_modificabile", $"La partita è {state.Status}: i punteggi non si possono cambiare.");
        }

        var match = _matcher.Match(giocatore, state.Players);
        if (match.IsAmbiguous)
        {
            return Error("giocatore_ambiguo", $"«{giocatore}» corrisponde a più giocatori: chiedi all'host quale.", match.Candidates.Select(AssistantNames.ForModel).ToList());
        }

        if (match.PlayerId is not { } playerId)
        {
            return Error("giocatore_sconosciuto", $"Nessun giocatore si chiama «{giocatore}».", state.Players.Select(p => AssistantNames.ForModel(p.Name)).ToList());
        }

        var resolvedDimension = ResolveDimension(dimensione, state.Dimensions);
        if (resolvedDimension is null)
        {
            return dimensione is null
                ? Error("dimensione_richiesta", "La partita ha più dimensioni di punteggio: chiedi all'host quale.", state.Dimensions)
                : Error("dimensione_sconosciuta", $"La dimensione «{dimensione}» non esiste.", state.Dimensions);
        }

        var targetRound = round ?? state.CurrentRound;
        if (targetRound < 1)
        {
            return Error("round_non_valido", "Il round parte da 1.");
        }

        var before = state.Scores
            .Where(s => s.PlayerId == playerId && s.Round == targetRound
                && string.Equals(s.Dimension, resolvedDimension, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .FirstOrDefault();
        var receipt = await _sink.ProposeScoreAsync(
            new ScoreProposalDraft(playerId, AssistantNames.ForModel(match.Name ?? giocatore), resolvedDimension, targetRound, punti, before),
            cancellationToken).ConfigureAwait(false);
        return new ProposalCreated(receipt.ProposalId, receipt.Summary, PendingNote);
    }

    public async Task<object> ProposeTurnAsync(CancellationToken cancellationToken = default)
    {
        var state = await _reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.CanAdvanceTurn)
        {
            return Error("sessione_non_in_corso", $"La partita è {state.Status}: il turno passa solo a partita in corso.");
        }

        var receipt = await _sink.ProposeTurnAsync(
            new TurnProposalDraft(state.CurrentTurnIndex, state.CurrentTurnPlayerId), cancellationToken).ConfigureAwait(false);
        return new ProposalCreated(receipt.ProposalId, receipt.Summary, PendingNote);
    }

    private static JsonSerializerOptions CreateToolJson()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { WriteIndented = false };
        options.MakeReadOnly();
        return options;
    }

    private AssistantToolError Error(string code, string message, IReadOnlyList<string>? candidates = null)
    {
        Interlocked.Increment(ref _errorCount);
        return new AssistantToolError(code, message, candidates);
    }

    private static bool SamePassage(ChunkCitation a, ChunkCitation b) =>
        string.Equals(a.DocumentId, b.DocumentId, StringComparison.Ordinal)
        && a.PageNumber == b.PageNumber
        && string.Equals(a.SnippetPreview, b.SnippetPreview, StringComparison.Ordinal);

    private static string? ResolveDimension(string? requested, IReadOnlyList<string> dimensions)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return dimensions.Count == 1 ? dimensions[0] : null;
        }

        return dimensions.FirstOrDefault(d => string.Equals(d, requested.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
