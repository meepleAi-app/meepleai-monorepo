using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>
/// Una domanda del set come la scrive l'utente in <c>reference-questions-it.json</c>. Il gioco è un titolo
/// del catalogo, non un id: il seeder lo risolve nell'ambiente.
/// </summary>
internal sealed record ReferenceQuestionDefinition(
    string Code,
    string Game,
    string Question,
    string ExpectedAnswer,
    string Category,
    string Difficulty,
    string QuestionType,
    string SourceReference,
    bool OutOfCorpus,
    bool InGate);

/// <summary>
/// #4470 (ADR-095 fetta 7b): legge il set delle domande di riferimento, risorsa incorporata dell'API.
/// Il file sta in <c>Infrastructure/Seeders/ReferenceQuestions/Data/</c> e non in <c>tests/</c>, perché
/// l'immagine dell'API copia solo <c>apps/api/src/Api</c> e <c>data/</c>.
/// </summary>
/// <remarks>
/// Dei campi del set si leggono solo quelli che vanno nella tabella. <c>verifica</c>,
/// <c>ground_truth_originale</c> e <c>question_originale</c> documentano la verifica fatta con l'utente e
/// restano nel file.
/// </remarks>
internal static class ReferenceQuestionSet
{
    internal const string ResourceName =
        "Api.Infrastructure.Seeders.ReferenceQuestions.Data.reference-questions-it.json";

    public static IReadOnlyList<ReferenceQuestionDefinition> LoadEmbedded()
    {
        using var stream = typeof(ReferenceQuestionSet).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException(
                $"Embedded resource '{ResourceName}' not found: check the <EmbeddedResource> include in Api.csproj.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// Legge e controlla il set. Un set con anche un solo problema si rifiuta per intero, e l'eccezione li
    /// elenca tutti: caricarne una parte lascerebbe un gate che confronta insiemi diversi senza dirlo.
    /// </summary>
    /// <exception cref="InvalidDataException">Il file non è una lista di domande, o una domanda non è valida.</exception>
    internal static IReadOnlyList<ReferenceQuestionDefinition> Parse(string json)
    {
        List<RawQuestion?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RawQuestion?>>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The reference question set is not a JSON list of questions: {ex.Message}", ex);
        }

        if (raw is null)
            throw new InvalidDataException("The reference question set is empty (null).");

        var problems = new List<string>();
        var questions = new List<ReferenceQuestionDefinition>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var question = raw[i];
            if (question is null)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"question #{i + 1} is null"));
                continue;
            }

            var label = string.IsNullOrWhiteSpace(question.Id)
                ? string.Create(CultureInfo.InvariantCulture, $"question #{i + 1}")
                : question.Id;
            var before = problems.Count;
            Check(problems, label, "id", question.Id, ReferenceQuestion.CodeMaxLength);
            Check(problems, label, "game", question.Game, maxLength: null);
            Check(problems, label, "question", question.Question, ReferenceQuestion.QuestionMaxLength);
            Check(problems, label, "ground_truth", question.GroundTruth, ReferenceQuestion.ExpectedAnswerMaxLength);
            Check(problems, label, "category", question.Category, ReferenceQuestion.CategoryMaxLength);
            Check(problems, label, "difficulty", question.Difficulty, ReferenceQuestion.DifficultyMaxLength);
            Check(problems, label, "question_type", question.QuestionType, ReferenceQuestion.QuestionTypeMaxLength);
            Check(problems, label, "context_reference", question.ContextReference, ReferenceQuestion.SourceReferenceMaxLength);
            if (problems.Count > before)
                continue;

            questions.Add(new ReferenceQuestionDefinition(
                question.Id!, question.Game!, question.Question!, question.GroundTruth!, question.Category!,
                question.Difficulty!, question.QuestionType!, question.ContextReference!,
                question.OutOfCorpus ?? false, question.Gate ?? false));
        }

        problems.AddRange(questions
            .GroupBy(q => q.Code, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => string.Create(CultureInfo.InvariantCulture, $"id '{g.Key}' appears {g.Count()} times")));

        if (problems.Count > 0)
            throw new InvalidDataException(
                "The reference question set is not valid: " + string.Join("; ", problems));

        return questions;
    }

    private static void Check(List<string> problems, string label, string field, string? value, int? maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            problems.Add($"{label}: '{field}' is blank");
        else if (maxLength is { } max && value.Length > max)
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"{label}: '{field}' exceeds {max} characters"));
    }

    private sealed record RawQuestion(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("game")] string? Game,
        [property: JsonPropertyName("question")] string? Question,
        [property: JsonPropertyName("ground_truth")] string? GroundTruth,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("difficulty")] string? Difficulty,
        [property: JsonPropertyName("question_type")] string? QuestionType,
        [property: JsonPropertyName("context_reference")] string? ContextReference,
        [property: JsonPropertyName("fuori_dal_corpus")] bool? OutOfCorpus,
        [property: JsonPropertyName("gate")] bool? Gate);
}
