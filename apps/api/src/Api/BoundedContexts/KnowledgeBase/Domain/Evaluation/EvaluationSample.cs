namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// A single Q&amp;A sample for RAG evaluation.
/// Format aligned with both Mozilla Structured QA and MeepleAI custom datasets.
/// </summary>
internal sealed record EvaluationSample
{
    /// <summary>
    /// Unique identifier for the sample.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The question to ask the RAG system.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// The expected correct answer.
    /// </summary>
    public required string ExpectedAnswer { get; init; }

    /// <summary>
    /// Source document URL or path (for Mozilla dataset) or game ID (for MeepleAI).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Page number in source document where answer is found.
    /// </summary>
    public int? SourcePage { get; init; }

    /// <summary>
    /// Section or heading in source document.
    /// </summary>
    public string? Section { get; init; }

    /// <summary>
    /// Difficulty level: easy, medium, hard, edge_case.
    /// </summary>
    public string Difficulty { get; init; } = "medium";

    /// <summary>
    /// Category: setup, gameplay, scoring, edge_cases, clarification.
    /// </summary>
    public string Category { get; init; } = "gameplay";

    /// <summary>
    /// Game ID for MeepleAI custom dataset.
    /// </summary>
    public string? GameId { get; init; }

    /// <summary>
    /// Keywords that must appear in correct answer.
    /// </summary>
    public IReadOnlyList<string> ExpectedKeywords { get; init; } = [];

    /// <summary>
    /// IDs of relevant chunks that should be retrieved.
    /// Used for Recall@K calculation.
    /// </summary>
    public IReadOnlyList<string> RelevantChunkIds { get; init; } = [];

    /// <summary>
    /// Dataset source: mozilla, meepleai_custom.
    /// </summary>
    public string DatasetSource { get; init; } = "meepleai_custom";

    /// <summary>
    /// Creates a sample for Mozilla Structured QA format.
    /// </summary>
    public static EvaluationSample FromMozilla(
        string id,
        string question,
        string answer,
        string documentUrl,
        string section)
    {
        return new EvaluationSample
        {
            Id = id,
            Question = question,
            ExpectedAnswer = answer,
            Source = documentUrl,
            Section = section,
            DatasetSource = "mozilla",
            Difficulty = "medium",
            Category = "gameplay"
        };
    }

    /// <summary>
    /// Creates a sample for MeepleAI custom dataset format.
    /// </summary>
    public static EvaluationSample FromMeepleAI(
        string id,
        string question,
        string expectedAnswer,
        string gameId,
        int sourcePage,
        string difficulty,
        string category,
        IReadOnlyList<string>? expectedKeywords = null,
        IReadOnlyList<string>? relevantChunkIds = null)
    {
        return new EvaluationSample
        {
            Id = id,
            Question = question,
            ExpectedAnswer = expectedAnswer,
            GameId = gameId,
            SourcePage = sourcePage,
            Difficulty = difficulty,
            Category = category,
            ExpectedKeywords = expectedKeywords ?? [],
            RelevantChunkIds = relevantChunkIds ?? [],
            DatasetSource = "meepleai_custom"
        };
    }
}
