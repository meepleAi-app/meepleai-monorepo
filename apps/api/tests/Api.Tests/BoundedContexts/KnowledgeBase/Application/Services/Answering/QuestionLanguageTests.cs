using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4480: la lingua di una domanda breve, e quanto deve essere netta per smentire la lingua che
/// l'interfaccia chiede. Nella misura del 2026-10-18 molte domande italiane, con
/// <c>Accept-Language: it</c>, erano state rilevate «con sicurezza» come spagnole o francesi, e la
/// risposta era arrivata in quella lingua.
/// <para>
/// Le domande italiane sono quelle del set di riferimento dell'utente, lette dal file del repository
/// (<c>tests/evaluation-datasets/reference-questions-it.json</c>): una domanda aggiunta al set entra
/// da sola in questi test. Le domande nelle altre lingue sono scritte qui, sugli stessi temi.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4480")]
public sealed class QuestionLanguageTests
{
    private const string ItalianInterface = "it";
    private const string EnglishInterface = "en-US,en;q=0.9";

    private readonly LanguageDetectionService _detector = new(NullLogger<LanguageDetectionService>.Instance);

    // ─── Il set di riferimento, tutto in italiano ───────────────────────────────

    public static TheoryData<string, string> ReferenceQuestions()
    {
        var data = new TheoryData<string, string>();
        foreach (var (id, question) in LoadReferenceQuestions())
        {
            data.Add(id, question);
        }

        return data;
    }

    private static List<(string Id, string Question)> LoadReferenceQuestions()
    {
        var path = Path.Combine(FindEvaluationDatasets(), "reference-questions-it.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var questions = document.RootElement.EnumerateArray()
            .Select(item => (item.GetProperty("id").GetString()!, item.GetProperty("question").GetString()!))
            .ToList();

        // Un file vuoto o spostato non deve passare per un set tutto verde.
        if (questions.Count == 0)
        {
            throw new InvalidOperationException($"Nessuna domanda in {path}.");
        }

        return questions;
    }

    [Theory]
    [MemberData(nameof(ReferenceQuestions))]
    public void AReferenceQuestion_IsAnsweredInItalian_WithAnItalianInterface(string id, string question)
    {
        AnswerLanguageResolver.Resolve(_detector, question, ItalianInterface).Language
            .Should().Be("it", "{0} è in italiano e l'interfaccia chiede l'italiano: «{1}»", id, question);
    }

    [Theory]
    [MemberData(nameof(ReferenceQuestions))]
    public void AReferenceQuestion_IsDetectedAsItalian_WithoutTheInterface(string id, string question)
    {
        // Il rilevatore da solo: conta quando la richiesta non porta una lingua, e la risposta
        // segue allora la lingua rilevata anche se il rilevamento non è sicuro.
        _detector.Detect(question).Language
            .Should().Be("it", "{0} è in italiano: «{1}»", id, question);
    }

    /// <summary>
    /// Le domande del set che con l'interfaccia in inglese hanno risposta in inglese: per loro il
    /// rilevamento non è abbastanza netto da smentire l'interfaccia. Ogni voce è una decisione presa
    /// guardando la domanda; una domanda nuova che non supera la soglia fa fallire il test, e va
    /// aggiunta qui con il motivo, oppure va corretto il rilevatore.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AnsweredInEnglishWithAnEnglishInterface =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Vuoto: con il margine misurato sull'inglese tutte le domande del set lo superano.
            // Prima della review di #4480 (margine sulla seconda lingua) restavano in inglese
            // CAR-21, GLO-06, GLO-10 e GLO-17.
        };

    [Theory]
    [MemberData(nameof(ReferenceQuestions))]
    public void AReferenceQuestion_WithAnEnglishInterface_IsAnsweredInItalian_UnlessListed(string id, string question)
    {
        var expected = AnsweredInEnglishWithAnEnglishInterface.ContainsKey(id) ? "en" : "it";

        AnswerLanguageResolver.Resolve(_detector, question, EnglishInterface).Language
            .Should().Be(expected, "{0} con l'interfaccia in inglese: «{1}»", id, question);
    }

    [Fact]
    public void TheEnglishInterfaceExceptions_NameQuestionsOfTheSet()
    {
        var ids = LoadReferenceQuestions().Select(question => question.Id).ToHashSet(StringComparer.Ordinal);

        AnsweredInEnglishWithAnEnglishInterface.Keys.Should().BeSubsetOf(ids,
            "un'eccezione per una domanda tolta dal set non protegge più niente");
    }

    // ─── Domande brevi in altre lingue: non devono regredire ────────────────────

    public static TheoryData<string, string> ForeignQuestions => new()
    {
        { "en", "How many cards does each player draw at the start of their turn?" },
        { "en", "Can I trade resources with the bank during another player's turn?" },
        { "en", "What happens when the draw deck runs out?" },
        { "en", "Is the robber moved when a seven is rolled?" },
        { "en", "Who goes first in the setup phase?" },
        { "en", "Can a player place a meeple on a road that is already claimed?" },
        { "en", "How do you score a completed city at the end of the game?" },
        { "en", "Do I take damage from a trap if I jump over it?" },
        { "en", "What is the hand limit for train cards?" },
        { "en", "When does the game end, and who wins a tie?" },
        { "en", "How do I win?" },

        { "de", "Wie viele Karten zieht jeder Spieler zu Beginn seines Zuges?" },
        { "de", "Darf ich während des Zuges eines anderen Spielers mit der Bank handeln?" },
        { "de", "Was passiert, wenn der Nachziehstapel leer ist?" },
        { "de", "Wann wird der Räuber bewegt?" },
        { "de", "Wer beginnt das Spiel?" },
        { "de", "Kann ich einen Meeple auf eine Straße setzen, die schon besetzt ist?" },
        { "de", "Wie werden fertige Städte am Ende des Spiels gewertet?" },
        { "de", "Muss ich Karten abwerfen, wenn eine Sieben gewürfelt wird?" },
        { "de", "Wie viele Siegpunkte bringt die längste Handelsstraße?" },
        { "de", "Ist das Tauschen von Rohstoffen zwischen zwei Spielern erlaubt?" },

        { "fr", "Combien de cartes chaque joueur pioche-t-il au début de son tour ?" },
        { "fr", "Puis-je échanger des ressources avec la banque pendant le tour d'un autre joueur ?" },
        { "fr", "Que se passe-t-il quand la pioche est vide ?" },
        { "fr", "Quand est-ce que le voleur est déplacé ?" },
        { "fr", "Qui commence la partie ?" },
        { "fr", "Est-ce que je peux poser un meeple sur une route déjà occupée ?" },
        { "fr", "Comment compte-t-on les points d'une ville terminée à la fin de la partie ?" },
        { "fr", "Faut-il défausser la moitié de ses cartes quand un sept est lancé ?" },
        { "fr", "Combien de points de victoire rapporte la route la plus longue ?" },
        { "fr", "Peut-on construire deux gares dans la même ville ?" },

        { "es", "¿Cuántas cartas roba cada jugador al comienzo de su turno?" },
        { "es", "¿Puedo comerciar recursos con la banca durante el turno de otro jugador?" },
        { "es", "¿Qué pasa cuando se acaba el mazo de robo?" },
        { "es", "¿Cuándo se mueve el ladrón?" },
        { "es", "¿Quién empieza la partida?" },
        { "es", "¿Puedo colocar un seguidor en un camino que ya está ocupado?" },
        { "es", "¿Cómo se puntúa una ciudad completada al final del juego?" },
        // Senza il punto di domanda rovesciato e senza accenti, come si scrive spesso.
        { "es", "Hay que descartar la mitad de las cartas cuando sale un siete?" },
        { "es", "Cuantos puntos de victoria da el camino mas largo?" },
        { "es", "¿Se pueden construir dos estaciones en la misma ciudad?" },
    };

    [Theory]
    [MemberData(nameof(ForeignQuestions))]
    public void AForeignQuestion_IsConfidentlyDetectedInItsLanguage(string language, string question)
    {
        _detector.Detect(question).Should().Be(new LanguageDetection(language, IsConfident: true));
    }

    [Theory]
    [MemberData(nameof(ForeignQuestions))]
    public void AForeignQuestion_WinsOverAnItalianAcceptLanguage(string language, string question)
    {
        AnswerLanguageResolver.Resolve(_detector, question, "it-IT,it;q=0.9")
            .Should().Be(new AnswerLanguage(language, AnswerLanguageSource.Detected));
    }

    [Theory]
    [MemberData(nameof(ForeignQuestions))]
    public void AForeignQuestion_WinsOverAnItalianRequestedLanguage(string language, string question)
    {
        AnswerLanguageResolver.Resolve(_detector, question, acceptLanguage: null, requestedLanguage: ItalianInterface)
            .Should().Be(new AnswerLanguage(language, AnswerLanguageSource.Detected));
    }

    // ─── Quanto deve essere netto il rilevamento per smentire l'interfaccia ─────
    //
    // Lo scarto si misura sulla lingua che l'interfaccia chiede, cioè quella da smentire: una domanda
    // italiana breve ha spesso parole anche francesi o spagnole («il», «si», «un»), ma quasi nessuna
    // inglese, e con l'interfaccia in inglese l'italiano deve vincere.

    [Theory]
    [InlineData("Come funziona il Largest Army?")]
    [InlineData("Quando si usa il Ladro?")]
    [InlineData("Come si gioca il Monopolio?")]
    [InlineData("Quante carte si pescano?")]
    [InlineData("Posso usare il Brigante?")]
    [InlineData("Una carta per turno?")]
    public void AShortItalianQuestion_WinsOverAnEnglishInterface(string question)
    {
        AnswerLanguageResolver.Resolve(_detector, question, EnglishInterface)
            .Should().Be(new AnswerLanguage("it", AnswerLanguageSource.Detected));
    }

    [Fact]
    public void TheLanguageToBeat_IsTheRequestedOne_NotAcceptLanguage()
    {
        // «si» e «il» sono anche francesi: l'italiano stacca il francese di un punto e l'inglese di
        // tre. La lingua da smentire è quella chiesta dal client, che vale più di Accept-Language.
        AnswerLanguageResolver.Resolve(_detector, "Quando si usa il Ladro?", "it-IT", requestedLanguage: "en")
            .Should().Be(new AnswerLanguage("it", AnswerLanguageSource.Detected));
    }

    [Fact]
    public void AQuestionWithLittleToTellTheLanguagesApart_KeepsTheInterfaceLanguage()
    {
        // «una», «carta» e «turno» sono anche spagnole: sullo spagnolo, la lingua da smentire,
        // l'italiano ha in più soltanto «per». Troppo poco per rispondere in un'altra lingua da quella
        // che l'interfaccia chiede. La stessa domanda con l'interfaccia in inglese ha risposta in
        // italiano (sopra): lì le parole che la separano sono quattro contro una.
        AnswerLanguageResolver.Resolve(_detector, "Una carta per turno?", "es")
            .Should().Be(new AnswerLanguage("es", AnswerLanguageSource.AcceptLanguage));
    }

    [Fact]
    public void WordsAndCharacterPatternsThatDisagree_AreNotConfident()
    {
        // Parole funzionali inglesi attorno a parole lunghe italiane: le parole dicono inglese, gli
        // n-grammi di caratteri italiano. Quando i due segnali non concordano il rilevamento non è
        // sicuro, e la lingua dell'interfaccia resta.
        const string text = "the and of with from precipitevolissimevolmente sovrintendenza costituzionalità";

        _detector.Detect(text).IsConfident.Should().BeFalse();
        AnswerLanguageResolver.Resolve(_detector, text, ItalianInterface)
            .Should().Be(new AnswerLanguage("it", AnswerLanguageSource.AcceptLanguage));
    }

    // ─── Il profilo degli n-grammi ──────────────────────────────────────────────

    [Fact]
    public void ABrokenCharacterProfile_FailsWhenTheServiceIsBuilt_NotAtTheFirstQuestion()
    {
        // Un profilo che non si carica deve fermare l'avvio, non togliere in silenzio la sicurezza a
        // ogni domanda (con un warning per domanda) per tutta la vita del processo.
        var build = () => new LanguageDetectionService(
            NullLogger<LanguageDetectionService>.Instance,
            loadCharacterProfile: () => throw new InvalidOperationException("profilo rotto"));

        build.Should().Throw<InvalidOperationException>().WithMessage("profilo rotto");
    }

    private static string FindEvaluationDatasets()
    {
        // Come EvaluationSeedDatasetTests: si risale dalla cartella dei binari alla radice del
        // repository. Il file resta uno solo: copiarlo come fixture lo farebbe divergere dal set.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "evaluation-datasets")))
        {
            directory = directory.Parent;
        }

        return directory is not null
            ? Path.Combine(directory.FullName, "tests", "evaluation-datasets")
            : throw new InvalidOperationException("Cartella tests/evaluation-datasets non trovata risalendo da " + AppContext.BaseDirectory);
    }
}
