using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminReferenceQuestions;

/// <summary>
/// #4470 (ADR-095 fetta 7b, D8): le domande di riferimento, in sola lettura nella sezione Qualità. Filtri
/// facoltativi: il gioco del catalogo, e se la domanda è nel gate di pubblicazione.
/// </summary>
internal sealed record GetAdminReferenceQuestionsQuery(Guid? GameId, bool? InGate)
    : IQuery<AdminReferenceQuestionsDto>;

/// <summary>
/// Le domande che passano i filtri, e i giochi che hanno domande con quante ne hanno, senza filtri: servono
/// a scegliere il gioco per nome.
/// </summary>
internal sealed record AdminReferenceQuestionsDto(
    List<AdminReferenceQuestionDto> Items,
    List<AdminReferenceQuestionGameDto> Games);

/// <summary>Una domanda di riferimento. Il nome del gioco è nullo se il gioco non è più nel catalogo.</summary>
internal sealed record AdminReferenceQuestionDto(
    string Code,
    Guid GameId,
    string? GameName,
    string Question,
    string ExpectedAnswer,
    string Category,
    string Difficulty,
    string QuestionType,
    string SourceReference,
    bool OutOfCorpus,
    bool InGate);

internal sealed record AdminReferenceQuestionGameDto(Guid GameId, string? GameName, int QuestionCount);
