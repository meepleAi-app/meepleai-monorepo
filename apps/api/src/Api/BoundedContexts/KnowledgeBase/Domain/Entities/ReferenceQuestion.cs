using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// #4470 (ADR-095 fetta 7b, D7): una domanda di riferimento dell'agente, con la risposta attesa e il gioco
/// del catalogo su cui va posta. Il set lo scrive l'utente; il seeder lo carica e lo tiene allineato per
/// <see cref="Code"/>, che è il nome con cui due valutazioni confrontano la stessa domanda.
/// </summary>
public sealed class ReferenceQuestion : AggregateRoot<Guid>
{
    public const int CodeMaxLength = 32;
    public const int QuestionMaxLength = 1000;
    public const int ExpectedAnswerMaxLength = 4000;
    public const int CategoryMaxLength = 100;
    public const int DifficultyMaxLength = 32;
    public const int QuestionTypeMaxLength = 100;
    public const int SourceReferenceMaxLength = 300;

    /// <summary>Il codice della domanda nel set («CAT-01»): unico, non cambia.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Il gioco del catalogo (<c>shared_games</c>): quello che la chat riceve come <c>gameId</c>.</summary>
    public Guid SharedGameId { get; private set; }

    public string Question { get; private set; } = string.Empty;
    public string ExpectedAnswer { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Difficulty { get; private set; } = string.Empty;
    public string QuestionType { get; private set; } = string.Empty;

    /// <summary>Dove il manuale dà la risposta (<c>context_reference</c> del set).</summary>
    public string SourceReference { get; private set; } = string.Empty;

    /// <summary>
    /// La domanda chiede ciò che i documenti non contengono (<c>fuori_dal_corpus</c> del set): misura se
    /// l'agente inventa.
    /// </summary>
    public bool OutOfCorpus { get; private set; }

    /// <summary>La domanda fa parte del gate di pubblicazione (<c>gate</c> del set). Il sottoinsieme lo approva l'utente.</summary>
    public bool InGate { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ReferenceQuestion() : base() { }

    public static ReferenceQuestion Create(string code, ReferenceQuestionContent content, DateTime utcNow)
    {
        RequireText(code, CodeMaxLength, nameof(code));
        Validate(content);

        var question = new ReferenceQuestion
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = utcNow,
        };
        question.Apply(content, utcNow);
        return question;
    }

    /// <summary>
    /// Prende il contenuto del set. Restituisce <c>false</c>, e non tocca <see cref="UpdatedAt"/>, se era
    /// già uguale: il seeder lo chiama a ogni avvio, e una riga uguale non va riscritta.
    /// </summary>
    public bool Update(ReferenceQuestionContent content, DateTime utcNow)
    {
        Validate(content);
        if (content == CurrentContent())
            return false;

        Apply(content, utcNow);
        return true;
    }

    private ReferenceQuestionContent CurrentContent() => new(
        SharedGameId, Question, ExpectedAnswer, Category, Difficulty, QuestionType, SourceReference,
        OutOfCorpus, InGate);

    private void Apply(ReferenceQuestionContent content, DateTime utcNow)
    {
        SharedGameId = content.SharedGameId;
        Question = content.Question;
        ExpectedAnswer = content.ExpectedAnswer;
        Category = content.Category;
        Difficulty = content.Difficulty;
        QuestionType = content.QuestionType;
        SourceReference = content.SourceReference;
        OutOfCorpus = content.OutOfCorpus;
        InGate = content.InGate;
        UpdatedAt = utcNow;
    }

    private static void Validate(ReferenceQuestionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.SharedGameId == Guid.Empty)
            throw new ArgumentException("A reference question needs the catalog game it is asked on.", nameof(content));

        RequireText(content.Question, QuestionMaxLength, nameof(content.Question));
        RequireText(content.ExpectedAnswer, ExpectedAnswerMaxLength, nameof(content.ExpectedAnswer));
        RequireText(content.Category, CategoryMaxLength, nameof(content.Category));
        RequireText(content.Difficulty, DifficultyMaxLength, nameof(content.Difficulty));
        RequireText(content.QuestionType, QuestionTypeMaxLength, nameof(content.QuestionType));
        RequireText(content.SourceReference, SourceReferenceMaxLength, nameof(content.SourceReference));
    }

    private static void RequireText(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} must not be blank.", name);
        if (value.Length > maxLength)
            throw new ArgumentException($"{name} must not exceed {maxLength} characters.", name);
    }
}

/// <summary>Tutto ciò che di una domanda di riferimento viene dal set, tranne il codice.</summary>
public sealed record ReferenceQuestionContent(
    Guid SharedGameId,
    string Question,
    string ExpectedAnswer,
    string Category,
    string Difficulty,
    string QuestionType,
    string SourceReference,
    bool OutOfCorpus,
    bool InGate);
