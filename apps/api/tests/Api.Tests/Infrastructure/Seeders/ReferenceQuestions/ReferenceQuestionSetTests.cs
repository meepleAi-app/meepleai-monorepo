using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure.Seeders.ReferenceQuestions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// #4470: il set delle domande di riferimento è una risorsa dell'API, perché l'immagine Docker copia solo
/// <c>apps/api/src/Api</c> e <c>data/</c>. Questi test leggono la risorsa vera: se il file non entra
/// nell'assembly, o una domanda non si legge, il seeder non caricherebbe nulla e lo direbbe solo nel log.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class ReferenceQuestionSetTests
{
    // ── La risorsa vera ─────────────────────────────────────────────────────────

    [Fact]
    public void TheEmbeddedSet_ReadsEveryQuestionOfTheFile_InOrder()
    {
        var rawIds = RawIdsOfTheEmbeddedFile();

        var set = ReferenceQuestionSet.LoadEmbedded();

        rawIds.Should().NotBeEmpty();
        set.Select(q => q.Code).Should().Equal(rawIds);
    }

    [Fact]
    public void TheEmbeddedSet_HasUniqueCodes_AndNoBlankRequiredField()
    {
        var set = ReferenceQuestionSet.LoadEmbedded();

        set.Select(q => q.Code).Should().OnlyHaveUniqueItems();
        set.Should().AllSatisfy(q =>
        {
            q.Code.Should().NotBeNullOrWhiteSpace();
            q.Game.Should().NotBeNullOrWhiteSpace(q.Code);
            q.Question.Should().NotBeNullOrWhiteSpace(q.Code);
            q.ExpectedAnswer.Should().NotBeNullOrWhiteSpace(q.Code);
            q.Category.Should().NotBeNullOrWhiteSpace(q.Code);
            q.Difficulty.Should().NotBeNullOrWhiteSpace(q.Code);
            q.QuestionType.Should().NotBeNullOrWhiteSpace(q.Code);
            q.SourceReference.Should().NotBeNullOrWhiteSpace(q.Code);
        });
    }

    [Fact]
    public void TheEmbeddedSet_NamesOnlyCatalogTitles_AndNeverTheCatanDuplicate()
    {
        var set = ReferenceQuestionSet.LoadEmbedded();

        set.Select(q => q.Game).Distinct().Should().BeEquivalentTo(
            ["Catan", "Carcassonne", "Ticket to Ride", "Ticket to Ride: Europe", "Gloomhaven"],
            "«The Settlers of Catan» è un doppione del catalogo senza documenti");
        set.Single(q => q.Code == "TTR-18").Game.Should().Be("Ticket to Ride: Europe",
            "solo il regolamento Europe dice che i biglietti tenuti non si scartano più");
    }

    // ── Il gate di pubblicazione ────────────────────────────────────────────────

    /// <summary>
    /// Il sottoinsieme che l'utente ha approvato il 2026-10-29: per ogni gioco un fatto, un caso limite e un
    /// fuori ambito. Si elenca per nome e non per conteggio, così chi cambia il gate deve cambiare anche
    /// questa riga, e la ragione sta nel diff.
    /// </summary>
    private static readonly string[] ApprovedGate =
    [
        "CAT-12", "CAT-02", "CAT-30",
        "CAR-03", "CAR-19", "CAR-07",
        "TTR-11", "TTR-03", "TTR-07",
        "GLO-17", "GLO-02", "GLO-08",
    ];

    [Fact]
    public void TheEmbeddedSet_PutsInTheGateExactlyTheQuestionsTheUserApproved()
    {
        var set = ReferenceQuestionSet.LoadEmbedded();

        set.Where(q => q.InGate).Select(q => q.Code).Should().BeEquivalentTo(
            ApprovedGate,
            "il gate è quello approvato dall'utente: una domanda in più o in meno cambia ciò che blocca la pubblicazione");
    }

    [Fact]
    public void TheGate_HasThreeQuestionsPerBaseGame_OneOutOfScope_AndNoneOnEurope()
    {
        var gate = ReferenceQuestionSet.LoadEmbedded().Where(q => q.InGate).ToList();

        gate.Select(q => q.Game).Distinct().Should().BeEquivalentTo(
            ["Catan", "Carcassonne", "Ticket to Ride", "Gloomhaven"],
            "Ticket to Ride resta sul gioco base: il gate non deve dipendere da un quinto gioco");
        foreach (var perGame in gate.GroupBy(q => q.Game))
        {
            perGame.Should().HaveCount(3, perGame.Key);
            perGame.Count(q => q.QuestionType.StartsWith("Out of Scope", StringComparison.Ordinal))
                .Should().Be(1, $"{perGame.Key}: serve un fuori ambito che misuri se l'agente inventa");
        }
        gate.Should().OnlyContain(
            q => !q.OutOfCorpus,
            "una domanda del gate deve avere la risposta nei documenti, o il gate misurerebbe altro");
    }

    // ── La lettura ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ReadsTheUserFields_AndTheOptionalFlags()
    {
        var set = ReferenceQuestionSet.Parse(Json(
            Question("CAT-01", extra: """, "fuori_dal_corpus": true, "gate": true"""),
            Question("CAT-02")));

        set.Should().HaveCount(2);
        var first = set[0];
        first.Code.Should().Be("CAT-01");
        first.Game.Should().Be("Catan");
        first.Question.Should().Be("Domanda CAT-01?");
        first.ExpectedAnswer.Should().Be("Risposta CAT-01.");
        first.Category.Should().Be("Commercio");
        first.Difficulty.Should().Be("Medium");
        first.QuestionType.Should().Be("Procedurale");
        first.SourceReference.Should().Be("Manuale Catan - Commercio");
        first.OutOfCorpus.Should().BeTrue();
        first.InGate.Should().BeTrue();

        set[1].OutOfCorpus.Should().BeFalse("il campo manca");
        set[1].InGate.Should().BeFalse("il sottoinsieme del gate lo approva l'utente: senza il campo, fuori");
    }

    [Fact]
    public void Parse_IgnoresTheVerificationFields()
    {
        var set = ReferenceQuestionSet.Parse(Json(Question("CAT-01", extra: """
            , "ground_truth_originale": "vecchia", "question_originale": "vecchia?",
            "verifica": { "esito": "conferma", "pagina": [3, 9], "nota": null }
            """)));

        set.Should().ContainSingle().Which.ExpectedAnswer.Should().Be("Risposta CAT-01.");
    }

    [Fact]
    public void Parse_RefusesADuplicateCode_AndNamesIt()
    {
        var parse = () => ReferenceQuestionSet.Parse(Json(Question("CAT-01"), Question("CAT-01")));

        parse.Should().Throw<InvalidDataException>().WithMessage("*CAT-01*");
    }

    [Fact]
    public void Parse_RefusesABlankRequiredField_AndNamesTheQuestionAndTheField()
    {
        var parse = () => ReferenceQuestionSet.Parse(Json(
            Question("CAT-01"),
            Question("CAT-02", answer: "  ")));

        parse.Should().Throw<InvalidDataException>().WithMessage("*CAT-02*ground_truth*");
    }

    [Fact]
    public void Parse_RefusesAQuestionWithoutACode()
    {
        var parse = () => ReferenceQuestionSet.Parse(Json(Question("")));

        parse.Should().Throw<InvalidDataException>().WithMessage("*id*");
    }

    [Fact]
    public void Parse_RefusesATextLongerThanItsColumn()
    {
        var tooLong = new string('a', ReferenceQuestion.ExpectedAnswerMaxLength + 1);

        var parse = () => ReferenceQuestionSet.Parse(Json(Question("CAT-01", answer: tooLong)));

        parse.Should().Throw<InvalidDataException>().WithMessage("*CAT-01*ground_truth*");
    }

    [Fact]
    public void Parse_RefusesAFileThatIsNotAList()
    {
        var parse = () => ReferenceQuestionSet.Parse("""{ "questions": [] }""");

        parse.Should().Throw<InvalidDataException>();
    }

    // ── Supporto ────────────────────────────────────────────────────────────────

    private static List<string> RawIdsOfTheEmbeddedFile()
    {
        using var stream = typeof(ReferenceQuestionSet).Assembly
            .GetManifestResourceStream(ReferenceQuestionSet.ResourceName);
        stream.Should().NotBeNull("il file deve essere una EmbeddedResource dell'API");
        using var document = JsonDocument.Parse(stream!);
        return document.RootElement.EnumerateArray()
            .Select(q => q.GetProperty("id").GetString()!)
            .ToList();
    }

    private static string Json(params string[] questions) => $"[{string.Join(",", questions)}]";

    private static string Question(string id, string? answer = null, string extra = "") => $$"""
        {
          "id": {{JsonSerializer.Serialize(id)}},
          "game": "Catan",
          "category": "Commercio",
          "difficulty": "Medium",
          "question": "Domanda {{id}}?",
          "ground_truth": {{JsonSerializer.Serialize(answer ?? $"Risposta {id}.")}},
          "context_reference": "Manuale Catan - Commercio",
          "question_type": "Procedurale"{{extra}}
        }
        """;
}
