using System.Linq;
using System.Text.RegularExpressions;
using NTextCat;

namespace Api.Services;

/// <summary>
/// Rilevamento locale della lingua di un testo, per le cinque lingue del prodotto (en, it, de, fr, es),
/// senza chiamate esterne.
/// <para>
/// #4480: il rilevamento usa due segnali indipendenti.
/// <list type="number">
/// <item><b>Le parole decidono la lingua.</b> Ogni lingua ha un lessico di parole funzionali (articoli,
/// preposizioni, pronomi, ausiliari, parole interrogative) e di parole generiche del gioco da tavolo:
/// ogni parola del testo che sta nel lessico di una lingua vale un punto per quella lingua, e le lettere
/// accentate mezzo punto. Una parola di più lingue («una», «carta», «turno») conta per tutte, quindi
/// non le separa. Prima di #4480 i lessici erano pochi termini con pesi a mano, e all'italiano
/// mancavano «di», «un», «una», «non», «posso», che lo spagnolo invece contava in parte: molte domande
/// italiane risultavano spagnole o francesi.</item>
/// <item><b>Gli n-grammi di caratteri</b> (NTextCat, lo stesso profilo Core14 delle foto dei gamebook,
/// ristretto alle cinque lingue) <b>non decidono la lingua: possono solo togliere la sicurezza</b>,
/// quando ne indicano un'altra. Sulle frasi brevi il loro ordine è affidabile, ma lo scarto fra la
/// prima lingua e la seconda è minimo e non dice quanto fidarsi.</item>
/// </list>
/// Il rilevamento è <i>sicuro</i> quando il testo ha almeno tre parole, la lingua rilevata stacca di
/// almeno <see cref="MinConfidentMargin"/> punti la lingua da battere, e gli n-grammi indicano la stessa
/// lingua. La sicurezza serve a una cosa sola: smentire la lingua che l'interfaccia chiede
/// (<c>AnswerLanguageResolver</c>), e la lingua da battere è proprio quella. Senza una lingua da battere,
/// o quando coincide con la rilevata, è la seconda per punteggio.
/// </para>
/// </summary>
internal class LanguageDetectionService : ILanguageDetectionService
{
    private const string DefaultLanguage = "en";

    /// <summary>Sotto questo numero di parole (di almeno due lettere) il rilevamento non è mai sicuro.</summary>
    private const int MinWords = 3;

    /// <summary>Un punteggio fino a questo valore non indica nessuna lingua: si risponde il default.</summary>
    private const double MinScore = 0.5;

    /// <summary>
    /// Di quanto la lingua rilevata deve staccare la lingua da battere per un rilevamento sicuro: più di
    /// una parola che l'altra non ha. Sulle domande in altre lingue di <c>QuestionLanguageTests</c> e su quelle
    /// inglesi dei dataset di valutazione lo scarto più piccolo misurato è 2.
    /// </summary>
    private const double MinConfidentMargin = 1.5;

    /// <summary>Il peso di una lettera accentata (o di un segno «¿», «¡») per ogni lingua che la usa.</summary>
    private const double DiacriticWeight = 0.5;

    /// <summary>Il profilo degli n-grammi, risorsa incorporata (vedi Api.csproj).</summary>
    private const string CharacterProfileResource = "Api.Resources.LanguageDetection.Core14.profile.xml";

    // L'ordine conta solo a parità di punteggio: vince la prima, e l'inglese è il default.
    private static readonly string[] SupportedLanguages = { "en", "it", "de", "fr", "es" };

    // FIX MA0009: Add timeout to prevent ReDoS attacks
    private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Il lessico di ogni lingua. Le lettere singole servono per le parole di una lettera («è», «e»,
    /// «y», «à») e per gli apostrofi: «dell'altro» si divide in «dell» e «altro», «l'ordine» in «l» e
    /// «ordine». Una parola che appartiene a più lingue va in ogni lessico: conta per tutte.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Lexicons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["en"] = """
            a i s t the an and or of to in on at for with by from about into over under after before during between
            is are was were be been being am do does did done don doesn didn isn aren can cannot could may might must
            shall should will would won not no yes if then than when what which who whom whose where why how many much
            more most less each every any all some other another same this that these those it its my your his her
            their our they them he she we you me there here only also still just get gets take takes happens happen per
            player players game games card cards turn turns deck board round rounds score scoring points point win wins
            tile tiles dice die hand piece pieces token tokens rule rules move play draw build end start first setup
            victory resource resources
            """,
        ["it"] = """
            a e è i o l d c il lo la gli le un uno una di del dello della dei degli delle dell al allo alla ai agli
            alle all da dal dallo dalla dai dagli dalle dall in nel nello nella nei negli nelle nell su sul sullo sulla
            sui sugli sulle sull con per tra fra ed od ma anche se che chi cosa cos come quando quanto quanta quanti
            quante quale quali dove dov perché perche non si ci ne mi ti vi sono sia essere ha hanno ho può puo possono
            posso puoi devo deve devono bisogna ogni ciascun ciascuno ciascuna più piu meno molto tutto tutti tutte
            altro altra altri altre stesso stessa questo questa questi queste quest quello quella quelli quelle quell
            suo sua suoi sue loro mio mia prima dopo durante ancora già gia solo sempre mai oppure cioè quindi poi
            viene vengono succede accade fare fa po
            gioco giocatore giocatori partita turno turni carta carte mazzo tessera tessere punti punto vittoria dadi
            dado mano pedina pedine tabellone plancia risorse mossa azione azioni regola regole fine inizio giocare
            pescare costruire scartare vince vincere obbligatorio possibile
            """,
        ["de"] = """
            der die das den dem des ein eine einen einem einer eines und oder aber ist sind war wird werden wurde kann
            können darf dürfen muss müssen soll sollen hat haben nicht kein keine keinen mit von zu zum zur auf in im
            an am bei beim für aus nach vor über unter zwischen während wenn wann wie was wer wo warum welche welcher
            welches jeder jede jedes jeden ich du er sie es wir man sich mich mir auch noch nur schon dass als alle
            alles andere anderen anderer sein seine seinen seines ihr ihre dann denn da mehr viele
            spieler spielerin spiel spiels zug zuges züge karte karten runde punkte sieg siegpunkte würfel stapel
            plättchen brett hand regel regeln rohstoffe ende beginn passiert gewinnt ziehen zieht spielen bauen
            """,
        ["fr"] = """
            à a y l d c j n s t qu le la les un une des du de au aux et ou où mais donc car ni en dans sur sous pour
            par avec sans entre pendant avant après vers chez que qui quoi quel quelle quels quelles est sont être été
            était ont avoir peut peuvent peux puis puisse doit doivent dois faut il ils elle elles on je tu nous vous
            se ne pas plus moins ce cet cette ces son sa ses leur leurs mon ma mes chaque tout tous toute toutes autre
            autres même comment combien quand pourquoi si aussi encore déjà seulement alors
            jeu joueur joueurs joueuse partie parties tour tours carte cartes pioche plateau points point victoire dé
            dés main pion pions tuile tuiles règle règles ressources fin début passe gagne piocher jouer construire
            obligatoire
            """,
        ["es"] = """
            y a o el la los las lo un una uno unos unas de del al en con sin por para sobre entre durante antes
            después desde hasta hacia que qué quien quién quienes cual cuál cuales cuáles cuando cuándo cuanto cuánto
            cuantos cuántos cuanta cuánta cuantas cuántas como cómo donde dónde porque es son está están estar ser era
            fue hay ha han he tiene tienen puede pueden puedo puedes debe deben debo hace hacer no sí si se me te le
            les nos su sus mi mis tu este esta estos estas ese esa eso otro otra otros otras mismo misma cada todo
            todos toda todas más mas menos muy también ya solo sólo pero ni
            jugador jugadores juego partida turno turnos carta cartas mazo ficha fichas tablero puntos punto victoria
            dados dado mano loseta losetas regla reglas recursos final inicio pasa gana robar jugar construir
            obligatorio posible
            """,
    }.ToDictionary(
        lexicon => lexicon.Key,
        lexicon => lexicon.Value.Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal),
        StringComparer.Ordinal);

    /// <summary>Le lettere accentate di ogni lingua, e per lo spagnolo i segni «¿» e «¡».</summary>
    private static readonly IReadOnlyDictionary<string, HashSet<char>> Diacritics = new Dictionary<string, HashSet<char>>(StringComparer.Ordinal)
    {
        ["en"] = new HashSet<char>(),
        ["it"] = new HashSet<char>("àèéìòù"),
        ["de"] = new HashSet<char>("äöüß"),
        ["fr"] = new HashSet<char>("àâçéèêëîïôùûœ"),
        ["es"] = new HashSet<char>("áéíñóúü¿¡"),
    };

    // Lo stesso profilo di NTextCatLanguageDetectionService (SessionTracking), qui con le sole cinque
    // lingue del prodotto: l'ordine fra loro non cambia, perché la distanza di ogni modello dal testo
    // non dipende dagli altri.
    private static readonly IReadOnlyDictionary<string, string> CharacterProfileLanguages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["eng"] = "en",
            ["ita"] = "it",
            ["deu"] = "de",
            ["fra"] = "fr",
            ["spa"] = "es",
        };

    // Caricato una volta per processo: i test costruiscono un'istanza per caso. PublicationOnly non
    // memorizza l'eccezione: un profilo che non si carica fallisce a ogni costruzione, non una volta
    // sola per poi spegnere in silenzio la sicurezza di ogni domanda.
    private static readonly Lazy<RankedLanguageIdentifier> SharedCharacterIdentifier =
        new(LoadCharacterIdentifier, LazyThreadSafetyMode.PublicationOnly);

    private readonly ILogger<LanguageDetectionService> _logger;
    private readonly RankedLanguageIdentifier _characterIdentifier;

    public LanguageDetectionService(ILogger<LanguageDetectionService> logger)
        : this(logger, () => SharedCharacterIdentifier.Value)
    {
    }

    /// <summary>
    /// Il profilo degli n-grammi si carica qui, non alla prima domanda: un profilo rotto ferma la
    /// costruzione del servizio (all'avvio, vedi <c>Program.cs</c>) invece di lasciarlo girare senza
    /// secondo segnale.
    /// </summary>
    internal LanguageDetectionService(
        ILogger<LanguageDetectionService> logger, Func<RankedLanguageIdentifier> loadCharacterProfile)
    {
        ArgumentNullException.ThrowIfNull(loadCharacterProfile);
        _logger = logger;
        _characterIdentifier = loadCharacterProfile();

        _logger.LogInformation("LanguageDetectionService initialized with 5 supported languages: {Languages}",
            string.Join(", ", SupportedLanguages));
    }

    /// <summary>
    /// La lingua del testo secondo le parole: la stessa di <see cref="Detect"/>. Non consulta gli
    /// n-grammi, che servono solo alla sicurezza.
    /// </summary>
    public async Task<string> DetectLanguageAsync(string text)
    {
        // Handle empty or whitespace text
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty or whitespace text provided, defaulting to 'en'");
            return DefaultLanguage;
        }

        try
        {
            var detectedLanguage = await Task.Run(() => ReadWords(text).Language).ConfigureAwait(false);

            _logger.LogInformation("Detected language: {Language} (text length: {Length})",
                detectedLanguage, text.Length);

            return detectedLanguage;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // FALLBACK PATTERN: Language detection failures default to English (en)
            // Rationale: Language detection is a best-effort enhancement for RAG and embedding
            // operations. Failing the entire operation because we cannot detect language would
            // reduce system availability. Defaulting to English is reasonable given it's the most
            // common language for board game rules and our primary training data language.
            // Context: Detection failures are typically edge cases (empty text, unsupported encoding)
            _logger.LogError(ex, "Error during language detection, defaulting to 'en'");
            return DefaultLanguage;
        }
    }

    /// <summary>
    /// Check if language code is in the supported list
    /// </summary>
    public bool IsSupportedLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        return SupportedLanguages.Contains(languageCode.ToLowerInvariant(), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public LanguageDetection Detect(string text, string? languageToBeat = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LanguageDetection(DefaultLanguage, IsConfident: false);
        }

        try
        {
            var words = ReadWords(text, languageToBeat?.Trim().ToLowerInvariant());
            var confident = words.HasClearMargin && CharacterPatternsAgree(text, words.Language);
            return new LanguageDetection(words.Language, confident);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Come DetectLanguageAsync: il rilevamento è un aiuto, non deve far fallire una risposta.
            _logger.LogError(ex, "Error during language detection, defaulting to 'en'");
            return new LanguageDetection(DefaultLanguage, IsConfident: false);
        }
    }

    /// <summary>
    /// La lingua con il punteggio più alto, e se stacca di <see cref="MinConfidentMargin"/> la lingua da
    /// battere: <paramref name="languageToBeat"/> se è un'altra lingua supportata, altrimenti la seconda.
    /// Meno di <see cref="MinWords"/> parole, o nessun punteggio significativo: il default, senza margine.
    /// </summary>
    private static WordReading ReadWords(string text, string? languageToBeat = null)
    {
        var lowercase = text.ToLowerInvariant();
        var words = WordRegex.Matches(lowercase).Select(match => match.Value).ToList();

        if (words.Count(word => word.Length > 1) < MinWords)
        {
            return new WordReading(DefaultLanguage, HasClearMargin: false);
        }

        // OrderByDescending è stabile: a parità vale l'ordine di SupportedLanguages.
        var ranked = SupportedLanguages
            .Select(language => (Language: language, Score:
                words.Count(word => Lexicons[language].Contains(word))
                + (DiacriticWeight * lowercase.Count(character => Diacritics[language].Contains(character)))))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        var best = ranked[0];
        if (best.Score <= MinScore)
        {
            return new WordReading(DefaultLanguage, HasClearMargin: false);
        }

        // #4480 (review): lo scarto conta sulla lingua che il rilevamento deve smentire. Una domanda
        // italiana breve ha spesso parole anche francesi o spagnole («il», «si»), e sulla seconda
        // lingua non arriva alla soglia; sull'inglese dell'interfaccia sì. Lo scarto sulla lingua da
        // battere non è mai minore di quello sulla seconda.
        var rivalIndex = ranked.FindIndex(
            1, candidate => string.Equals(candidate.Language, languageToBeat, StringComparison.Ordinal));
        var rivalScore = ranked[rivalIndex < 0 ? 1 : rivalIndex].Score;

        // I punteggi sono multipli di 0,5: la differenza è esatta in virgola mobile.
        return new WordReading(best.Language, best.Score - rivalScore >= MinConfidentMargin);
    }

    /// <summary>Se gli n-grammi di caratteri indicano la stessa lingua delle parole.</summary>
    private bool CharacterPatternsAgree(string text, string language)
    {
        try
        {
            var best = _characterIdentifier.Identify(text).FirstOrDefault();
            return best is not null
                && CharacterProfileLanguages.TryGetValue(best.Item1.Iso639_3, out var code)
                && string.Equals(code, language, StringComparison.Ordinal);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Senza il secondo segnale il rilevamento non è sicuro: decide la lingua dell'interfaccia.
            _logger.LogWarning(ex, "Character n-gram language check failed; the detection is not confident");
            return false;
        }
    }

    private static RankedLanguageIdentifier LoadCharacterIdentifier()
    {
        var assembly = typeof(LanguageDetectionService).Assembly;
        using var stream = assembly.GetManifestResourceStream(CharacterProfileResource)
            ?? throw new InvalidOperationException($"Embedded resource '{CharacterProfileResource}' not found.");

        return new RankedLanguageIdentifierFactory()
            .Load(stream, model => CharacterProfileLanguages.ContainsKey(model.Language.Iso639_3));
    }

    private readonly record struct WordReading(string Language, bool HasClearMargin);
}
