namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a test case from the golden dataset.
/// BGAI-059: Quality test implementation for accuracy validation.
/// Used to validate RAG system accuracy against expected answers, citations, and forbidden keywords.
/// </summary>
public record GoldenDatasetTestCase
{
    /// <summary>
    /// Unique identifier for this test case (e.g., "tm_001", "ws_002")
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The question to ask the RAG system
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Keywords that should appear in a correct answer
    /// </summary>
    public IReadOnlyList<string> ExpectedAnswerKeywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Expected citations (page numbers and snippet content)
    /// </summary>
    public IReadOnlyList<ExpectedCitation> ExpectedCitations { get; init; } = Array.Empty<ExpectedCitation>();

    /// <summary>
    /// Keywords that should NOT appear in the answer (hallucination indicators)
    /// </summary>
    public IReadOnlyList<string> ForbiddenKeywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Difficulty level: "easy", "medium", "hard"
    /// </summary>
    public string Difficulty { get; init; } = "easy";

    /// <summary>
    /// Category: "gameplay", "setup", "endgame", "edge_cases"
    /// </summary>
    public string Category { get; init; } = "gameplay";

    /// <summary>
    /// Game ID this test case belongs to (e.g., "terraforming-mars")
    /// </summary>
    public string GameId { get; init; } = string.Empty;

    /// <summary>
    /// Expert who annotated this test case
    /// </summary>
    public string AnnotatedBy { get; init; } = string.Empty;

    /// <summary>
    /// When this test case was annotated
    /// </summary>
    public DateTime AnnotatedAt { get; init; }

    /// <summary>
    /// Creates a new GoldenDatasetTestCase with validation
    /// </summary>
    public static GoldenDatasetTestCase Create(
        string id,
        string question,
        IReadOnlyList<string> expectedAnswerKeywords,
        IReadOnlyList<ExpectedCitation> expectedCitations,
        IReadOnlyList<string> forbiddenKeywords,
        string difficulty,
        string category,
        string gameId,
        string annotatedBy,
        DateTime annotatedAt)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Test case ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be empty", nameof(question));

        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        if (!IsValidDifficulty(difficulty))
            throw new ArgumentException($"Invalid difficulty: {difficulty}. Must be 'easy', 'medium', or 'hard'", nameof(difficulty));

        if (!IsValidCategory(category))
            throw new ArgumentException($"Invalid category: {category}. Must be 'gameplay', 'setup', 'endgame', or 'edge_cases'", nameof(category));

        return new GoldenDatasetTestCase
        {
            Id = id,
            Question = question,
            ExpectedAnswerKeywords = expectedAnswerKeywords ?? Array.Empty<string>(),
            ExpectedCitations = expectedCitations ?? Array.Empty<ExpectedCitation>(),
            ForbiddenKeywords = forbiddenKeywords ?? Array.Empty<string>(),
            Difficulty = difficulty.ToLowerInvariant(),
            Category = category.ToLowerInvariant(),
            GameId = gameId,
            AnnotatedBy = annotatedBy ?? string.Empty,
            AnnotatedAt = annotatedAt
        };
    }

    private static bool IsValidDifficulty(string difficulty)
    {
        var valid = new[] { "easy", "medium", "hard" };
        return valid.Contains(difficulty?.ToLowerInvariant(), StringComparer.Ordinal);
    }

    private static bool IsValidCategory(string category)
    {
        var valid = new[] { "gameplay", "setup", "endgame", "edge_case", "edge_cases" }; // Accept both singular and plural
        return valid.Contains(category?.ToLowerInvariant(), StringComparer.Ordinal);
    }
}

/// <summary>
/// Expected citation for a golden dataset test case
/// </summary>
public record ExpectedCitation
{
    /// <summary>
    /// Expected page number in the rulebook
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Text snippet that should be present in the citation
    /// </summary>
    public string SnippetContains { get; init; } = string.Empty;

    /// <summary>
    /// Creates a new ExpectedCitation with validation
    /// </summary>
    public static ExpectedCitation Create(int page, string snippetContains)
    {
        if (page <= 0)
            throw new ArgumentException("Page number must be positive", nameof(page));

        if (string.IsNullOrWhiteSpace(snippetContains))
            throw new ArgumentException("Snippet content cannot be empty", nameof(snippetContains));

        return new ExpectedCitation
        {
            Page = page,
            SnippetContains = snippetContains
        };
    }
}

/// <summary>
/// Result of evaluating a RAG response against a golden dataset test case
/// </summary>
public record AccuracyEvaluationResult
{
    /// <summary>
    /// Test case ID being evaluated
    /// </summary>
    public string TestCaseId { get; init; } = string.Empty;

    /// <summary>
    /// Whether all expected keywords were found in the answer
    /// </summary>
    public bool KeywordsMatch { get; init; }

    /// <summary>
    /// Number of expected keywords found / total expected
    /// </summary>
    public double KeywordMatchRate { get; init; }

    /// <summary>
    /// Whether citations reference the correct pages
    /// </summary>
    public bool CitationsValid { get; init; }

    /// <summary>
    /// Number of valid citations / total expected
    /// </summary>
    public double CitationValidityRate { get; init; }

    /// <summary>
    /// Whether any forbidden keywords were found (hallucination indicator)
    /// </summary>
    public bool NoForbiddenKeywords { get; init; }

    /// <summary>
    /// Combined verdict: correct if keywords match AND citations valid AND no forbidden keywords
    /// </summary>
    public bool IsCorrect { get; init; }

    /// <summary>
    /// RAG confidence score for this answer
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Difficulty level of the test case
    /// </summary>
    public string Difficulty { get; init; } = "easy";

    /// <summary>
    /// Category of the test case
    /// </summary>
    public string Category { get; init; } = "gameplay";

    /// <summary>
    /// Game ID for this test case
    /// </summary>
    public string GameId { get; init; } = string.Empty;

    /// <summary>
    /// Creates a new AccuracyEvaluationResult
    /// </summary>
    public static AccuracyEvaluationResult Create(
        string testCaseId,
        bool keywordsMatch,
        double keywordMatchRate,
        bool citationsValid,
        double citationValidityRate,
        bool noForbiddenKeywords,
        double confidenceScore,
        string difficulty,
        string category,
        string gameId)
    {
        var isCorrect = keywordsMatch && citationsValid && noForbiddenKeywords;

        return new AccuracyEvaluationResult
        {
            TestCaseId = testCaseId,
            KeywordsMatch = keywordsMatch,
            KeywordMatchRate = Math.Clamp(keywordMatchRate, 0.0, 1.0),
            CitationsValid = citationsValid,
            CitationValidityRate = Math.Clamp(citationValidityRate, 0.0, 1.0),
            NoForbiddenKeywords = noForbiddenKeywords,
            IsCorrect = isCorrect,
            ConfidenceScore = Math.Clamp(confidenceScore, 0.0, 1.0),
            Difficulty = difficulty,
            Category = category,
            GameId = gameId
        };
    }
}
