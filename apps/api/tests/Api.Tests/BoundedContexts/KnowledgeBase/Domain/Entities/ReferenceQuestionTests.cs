using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4470 (ADR-095 fetta 7b): una domanda di riferimento, con la sua risposta attesa e il gioco su cui va
/// posta. Il seeder la riscrive dal set a ogni avvio, quindi l'aggiornamento deve dire se qualcosa è
/// cambiato: una riga uguale non va toccata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class ReferenceQuestionTests
{
    private static readonly DateTime Monday = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday = Monday.AddDays(1);
    private static readonly Guid Catan = Guid.Parse("6b76bdb3-0000-0000-0000-000000000001");

    private static ReferenceQuestionContent Content(
        Guid? gameId = null,
        string question = "Posso giocare più di una carta Sviluppo nello stesso turno?",
        string expectedAnswer = "No, al massimo una per turno, salvo le carte Punto Vittoria.",
        string category = "Sviluppo / Condizioni di Vittoria",
        string difficulty = "Hard",
        string questionType = "Eccezione / Limite",
        string sourceReference = "Manuale Catan - Carte Sviluppo",
        bool outOfCorpus = false,
        bool inGate = false) =>
        new(gameId ?? Catan, question, expectedAnswer, category, difficulty, questionType,
            sourceReference, outOfCorpus, inGate);

    [Fact]
    public void Create_KeepsEveryField_AndStartsWithTheSameCreationAndUpdateTime()
    {
        var question = ReferenceQuestion.Create("CAT-02", Content(outOfCorpus: true, inGate: true), Monday);

        question.Id.Should().NotBe(Guid.Empty);
        question.Code.Should().Be("CAT-02");
        question.SharedGameId.Should().Be(Catan);
        question.Question.Should().Be("Posso giocare più di una carta Sviluppo nello stesso turno?");
        question.ExpectedAnswer.Should().Be("No, al massimo una per turno, salvo le carte Punto Vittoria.");
        question.Category.Should().Be("Sviluppo / Condizioni di Vittoria");
        question.Difficulty.Should().Be("Hard");
        question.QuestionType.Should().Be("Eccezione / Limite");
        question.SourceReference.Should().Be("Manuale Catan - Carte Sviluppo");
        question.OutOfCorpus.Should().BeTrue();
        question.InGate.Should().BeTrue();
        question.CreatedAt.Should().Be(Monday);
        question.UpdatedAt.Should().Be(Monday);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("question")]
    [InlineData("expectedAnswer")]
    [InlineData("category")]
    [InlineData("difficulty")]
    [InlineData("questionType")]
    [InlineData("sourceReference")]
    public void Create_RefusesABlankField(string field)
    {
        var code = field == "code" ? "  " : "CAT-02";
        var content = field switch
        {
            "question" => Content(question: " "),
            "expectedAnswer" => Content(expectedAnswer: ""),
            "category" => Content(category: " "),
            "difficulty" => Content(difficulty: ""),
            "questionType" => Content(questionType: " "),
            "sourceReference" => Content(sourceReference: ""),
            _ => Content(),
        };

        var create = () => ReferenceQuestion.Create(code, content, Monday);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RefusesAQuestionWithoutAGame()
    {
        var create = () => ReferenceQuestion.Create("CAT-02", Content(gameId: Guid.Empty), Monday);

        create.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void Create_RefusesATextLongerThanItsColumn()
    {
        var tooLong = new string('a', ReferenceQuestion.ExpectedAnswerMaxLength + 1);

        var create = () => ReferenceQuestion.Create("CAT-02", Content(expectedAnswer: tooLong), Monday);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WithTheSameContent_ChangesNothing()
    {
        var question = ReferenceQuestion.Create("CAT-02", Content(), Monday);

        var changed = question.Update(Content(), Tuesday);

        changed.Should().BeFalse();
        question.UpdatedAt.Should().Be(Monday, "una riga uguale al set non va toccata");
    }

    [Fact]
    public void Update_WithADifferentAnswer_TakesItAndMovesTheUpdateTime()
    {
        var question = ReferenceQuestion.Create("CAT-02", Content(), Monday);

        var changed = question.Update(Content(expectedAnswer: "Una sola carta Sviluppo per turno."), Tuesday);

        changed.Should().BeTrue();
        question.ExpectedAnswer.Should().Be("Una sola carta Sviluppo per turno.");
        question.UpdatedAt.Should().Be(Tuesday);
        question.CreatedAt.Should().Be(Monday);
        question.Code.Should().Be("CAT-02");
    }

    [Fact]
    public void Update_CanMoveTheQuestionToAnotherGame_AndIntoTheGate()
    {
        var europe = Guid.NewGuid();
        var question = ReferenceQuestion.Create("TTR-18", Content(), Monday);

        var changed = question.Update(Content(gameId: europe, inGate: true, outOfCorpus: true), Tuesday);

        changed.Should().BeTrue();
        question.SharedGameId.Should().Be(europe);
        question.InGate.Should().BeTrue();
        question.OutOfCorpus.Should().BeTrue();
    }

    [Fact]
    public void Update_RefusesInvalidContent_AndKeepsTheOldOne()
    {
        var question = ReferenceQuestion.Create("CAT-02", Content(), Monday);

        var update = () => question.Update(Content(question: ""), Tuesday);

        update.Should().Throw<ArgumentException>();
        question.Question.Should().Be("Posso giocare più di una carta Sviluppo nello stesso turno?");
        question.UpdatedAt.Should().Be(Monday);
    }
}
