using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;

/// <summary>
/// Domain service for loading and filtering golden dataset test cases.
/// BGAI-059: Quality test implementation for accuracy validation.
/// Loads test cases from tests/data/golden_dataset.json and adversarial_dataset.json
/// </summary>
public interface IGoldenDatasetLoader
{
    /// <summary>
    /// Loads all test cases from the golden dataset
    /// </summary>
    Task<IReadOnlyList<GoldenDatasetTestCase>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads test cases for a specific game
    /// </summary>
    Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByGameAsync(string gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads test cases by difficulty level
    /// </summary>
    Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByDifficultyAsync(string difficulty, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads test cases by category
    /// </summary>
    Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Samples N test cases from the dataset
    /// </summary>
    /// <param name="count">Number of cases to sample</param>
    /// <param name="stratified">If true, samples proportionally from each difficulty/category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<GoldenDatasetTestCase>> SampleAsync(int count, bool stratified = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads test cases by annotator (e.g., filter for expert-annotated cases)
    /// </summary>
    /// <param name="annotator">Annotator identifier to filter by</param>
    /// <param name="exclude">If true, excludes cases from this annotator; if false, includes only this annotator</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByAnnotatorAsync(string annotator, bool exclude = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IGoldenDatasetLoader
/// </summary>
public class GoldenDatasetLoader : IGoldenDatasetLoader
{
    private readonly string _datasetPath;
    private readonly ILogger<GoldenDatasetLoader> _logger;

    // Cache for loaded test cases
    private IReadOnlyList<GoldenDatasetTestCase>? _cachedTestCases;

    public GoldenDatasetLoader(ILogger<GoldenDatasetLoader> logger, string? datasetPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default to tests/data/golden_dataset.json from repository root
        if (datasetPath != null)
        {
            _datasetPath = datasetPath;
        }
        else
        {
            // Find repository root by looking for .git directory or solution file
            var currentDir = Directory.GetCurrentDirectory();
            var repoRoot = FindRepositoryRoot(currentDir);

            if (repoRoot == null)
            {
                _logger.LogWarning("Could not find repository root, using fallback path");
                _datasetPath = Path.Combine(currentDir, "tests", "data", "golden_dataset.json");
            }
            else
            {
                _datasetPath = Path.Combine(repoRoot, "tests", "data", "golden_dataset.json");
            }
        }

        _logger.LogDebug("GoldenDatasetLoader initialized with path: {Path}", _datasetPath);
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current != null)
        {
            // Check for .git directory - this marks the true repository root
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    public async Task<IReadOnlyList<GoldenDatasetTestCase>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTestCases != null)
        {
            _logger.LogDebug("Returning {Count} cached golden dataset test cases", _cachedTestCases.Count);
            return _cachedTestCases;
        }

        if (!File.Exists(_datasetPath))
        {
            _logger.LogWarning("Golden dataset file not found at {Path}", _datasetPath);
            return Array.Empty<GoldenDatasetTestCase>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_datasetPath, cancellationToken).ConfigureAwait(false);
            var dataset = JsonSerializer.Deserialize<GoldenDatasetFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dataset == null || dataset.Games == null)
            {
                _logger.LogWarning("Failed to parse golden dataset from {Path}", _datasetPath);
                return Array.Empty<GoldenDatasetTestCase>();
            }

            var testCases = new List<GoldenDatasetTestCase>();

            foreach (var game in dataset.Games)
            {
                if (game.TestCases == null) continue;

                foreach (var testCase in game.TestCases)
                {
                    var expectedCitations = testCase.ExpectedCitations?
                        .Select(c => ExpectedCitation.Create(c.Page, c.SnippetContains))
                        .ToList() ?? new List<ExpectedCitation>();

                    var goldenTestCase = GoldenDatasetTestCase.Create(
                        id: testCase.Id,
                        question: testCase.Question,
                        expectedAnswerKeywords: testCase.ExpectedAnswerKeywords ?? Array.Empty<string>(),
                        expectedCitations: expectedCitations,
                        forbiddenKeywords: testCase.ForbiddenKeywords ?? Array.Empty<string>(),
                        difficulty: testCase.Difficulty,
                        category: testCase.Category,
                        gameId: game.GameId,
                        annotatedBy: testCase.AnnotatedBy,
                        annotatedAt: DateTime.TryParse(testCase.AnnotatedAt, out var annotatedDate)
                            ? annotatedDate
                            : DateTime.UtcNow
                    );

                    testCases.Add(goldenTestCase);
                }
            }

            _cachedTestCases = testCases;
            _logger.LogInformation("Loaded {Count} golden dataset test cases from {GameCount} games",
                testCases.Count, dataset.Games.Length);

            return _cachedTestCases;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load golden dataset from {Path}", _datasetPath);
            return Array.Empty<GoldenDatasetTestCase>();
        }
    }

    public async Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByGameAsync(string gameId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        var allCases = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return allCases.Where(tc => tc.GameId.Equals(gameId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByDifficultyAsync(string difficulty, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            throw new ArgumentException("Difficulty cannot be empty", nameof(difficulty));

        var allCases = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return allCases.Where(tc => tc.Difficulty.Equals(difficulty, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        var allCases = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return allCases.Where(tc => tc.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<IReadOnlyList<GoldenDatasetTestCase>> SampleAsync(int count, bool stratified = true, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentException("Sample count must be positive", nameof(count));

        var allCases = await LoadAllAsync(cancellationToken).ConfigureAwait(false);

        if (count >= allCases.Count)
            return allCases;

        var random = new Random();

        if (!stratified)
        {
            // Simple random sampling
            return allCases.OrderBy(_ => random.Next()).Take(count).ToList();
        }

        // Stratified sampling: sample proportionally from each difficulty level
        var easy = allCases.Where(tc => string.Equals(tc.Difficulty, "easy", StringComparison.Ordinal)).ToList();
        var medium = allCases.Where(tc => string.Equals(tc.Difficulty, "medium", StringComparison.Ordinal)).ToList();
        var hard = allCases.Where(tc => string.Equals(tc.Difficulty, "hard", StringComparison.Ordinal)).ToList();

        var total = allCases.Count;
        var easyCount = (int)Math.Round((double)easy.Count / total * count);
        var mediumCount = (int)Math.Round((double)medium.Count / total * count);
        var hardCount = count - easyCount - mediumCount; // Remaining to reach exact count
        var sampled = new List<GoldenDatasetTestCase>();

        sampled.AddRange(easy.OrderBy(_ => random.Next()).Take(easyCount));
        sampled.AddRange(medium.OrderBy(_ => random.Next()).Take(mediumCount));
        sampled.AddRange(hard.OrderBy(_ => random.Next()).Take(hardCount));

        _logger.LogInformation("Stratified sampling: {Count} cases (easy: {Easy}, medium: {Medium}, hard: {Hard})",
            sampled.Count, easyCount, mediumCount, hardCount);

        return sampled;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByAnnotatorAsync(
        string annotator,
        bool exclude = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(annotator))
            throw new ArgumentException("Annotator cannot be empty", nameof(annotator));

        var allCases = await LoadAllAsync(cancellationToken).ConfigureAwait(false);

        var filtered = exclude
            ? allCases.Where(tc => !tc.AnnotatedBy.Equals(annotator, StringComparison.OrdinalIgnoreCase)).ToList()
            : allCases.Where(tc => tc.AnnotatedBy.Equals(annotator, StringComparison.OrdinalIgnoreCase)).ToList();

        _logger.LogInformation(
            "Loaded {Count} test cases by annotator filter (annotator: {Annotator}, exclude: {Exclude})",
            filtered.Count, annotator, exclude);

        return filtered;
    }
}

// JSON deserialization models
internal class GoldenDatasetFile
{
    [JsonPropertyName("metadata")]
    public GoldenDatasetMetadata? Metadata { get; set; }

    [JsonPropertyName("games")]
    public GoldenDatasetGame[]? Games { get; set; }
}

internal class GoldenDatasetMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("total_test_cases")]
    public int TotalTestCases { get; set; }
}

internal class GoldenDatasetGame
{
    [JsonPropertyName("game_id")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("game_name")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "it";

    [JsonPropertyName("test_cases")]
    public GoldenDatasetTestCaseJson[]? TestCases { get; set; }
}

internal class GoldenDatasetTestCaseJson
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("expected_answer_keywords")]
    public string[] ExpectedAnswerKeywords { get; set; } = Array.Empty<string>();

    [JsonPropertyName("expected_citations")]
    public ExpectedCitationJson[]? ExpectedCitations { get; set; }

    [JsonPropertyName("forbidden_keywords")]
    public string[] ForbiddenKeywords { get; set; } = Array.Empty<string>();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "easy";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "gameplay";

    [JsonPropertyName("annotated_by")]
    public string AnnotatedBy { get; set; } = string.Empty;

    [JsonPropertyName("annotated_at")]
    public string AnnotatedAt { get; set; } = string.Empty;
}

internal class ExpectedCitationJson
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("snippet_contains")]
    public string SnippetContains { get; set; } = string.Empty;
}
