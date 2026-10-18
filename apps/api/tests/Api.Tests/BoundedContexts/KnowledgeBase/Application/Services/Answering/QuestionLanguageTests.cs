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

    private readonly LanguageDetectionService _detector = new(NullLogger<LanguageDetectionService>.Instance);

    // ─── Il set di riferimento, tutto in italiano ───────────────────────────────

    public static TheoryData<string, string> ReferenceQuestions()
    {
        var path = Path.Combine(FindEvaluationDatasets(), "reference-questions-it.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var data = new TheoryData<string, string>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            data.Add(item.GetProperty("id").GetString()!, item.GetProperty("question").GetString()!);
        }

        // Un file vuoto o spostato non deve passare per un set tutto verde.
        if (data.Count == 0)
        {
            throw new InvalidOperationException($"Nessuna domanda in {path}.");
        }

        return data;
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

    [Fact]
    public void AQuestionWithLittleToTellTheLanguagesApart_KeepsTheInterfaceLanguage()
    {
        // «una», «carta» e «turno» sono anche spagnole: a separare l'italiano resta solo «per».
        // Troppo poco per rispondere in un'altra lingua da quella che l'interfaccia chiede.
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
