using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// A collection of evaluation samples forming a benchmark dataset.
/// Supports both Mozilla Structured QA and MeepleAI custom formats.
/// </summary>
public sealed class EvaluationDataset
{
    private readonly List<EvaluationSample> _samples = [];

    /// <summary>
    /// Dataset name for identification.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Dataset description.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Dataset version for tracking changes.
    /// </summary>
    public string Version { get; private set; } = "1.0.0";

    /// <summary>
    /// Source type: mozilla, meepleai_custom, combined.
    /// </summary>
    public string SourceType { get; private set; } = "meepleai_custom";

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// All samples in the dataset.
    /// </summary>
    public IReadOnlyList<EvaluationSample> Samples => _samples.AsReadOnly();

    /// <summary>
    /// Total number of samples.
    /// </summary>
    public int Count => _samples.Count;

    /// <summary>
    /// Creates an empty dataset.
    /// </summary>
    public static EvaluationDataset Create(string name, string description, string sourceType = "meepleai_custom")
    {
        return new EvaluationDataset
        {
            Name = name,
            Description = description,
            SourceType = sourceType,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds a sample to the dataset.
    /// </summary>
    public void AddSample(EvaluationSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (_samples.Any(s => s.Id == sample.Id))
        {
            throw new InvalidOperationException($"Sample with ID '{sample.Id}' already exists in dataset.");
        }

        _samples.Add(sample);
    }

    /// <summary>
    /// Adds multiple samples to the dataset.
    /// </summary>
    public void AddSamples(IEnumerable<EvaluationSample> samples)
    {
        foreach (var sample in samples)
        {
            AddSample(sample);
        }
    }

    /// <summary>
    /// Gets samples filtered by difficulty.
    /// </summary>
    public IReadOnlyList<EvaluationSample> GetByDifficulty(string difficulty)
    {
        return _samples
            .Where(s => s.Difficulty.Equals(difficulty, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets samples filtered by category.
    /// </summary>
    public IReadOnlyList<EvaluationSample> GetByCategory(string category)
    {
        return _samples
            .Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets samples filtered by game ID.
    /// </summary>
    public IReadOnlyList<EvaluationSample> GetByGameId(string gameId)
    {
        return _samples
            .Where(s => s.GameId?.Equals(gameId, StringComparison.OrdinalIgnoreCase) == true)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets samples filtered by dataset source.
    /// </summary>
    public IReadOnlyList<EvaluationSample> GetBySource(string datasetSource)
    {
        return _samples
            .Where(s => s.DatasetSource.Equals(datasetSource, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Validates the dataset meets minimum requirements.
    /// Phase 0 requires at least 30 Q&A pairs.
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Dataset name is required.");
        }

        if (_samples.Count < 30)
        {
            errors.Add($"Dataset must contain at least 30 samples. Current count: {_samples.Count}");
        }

        // Check for duplicate IDs
        var duplicateIds = _samples
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            errors.Add($"Duplicate sample IDs found: {string.Join(", ", duplicateIds)}");
        }

        // Check for empty questions/answers
        var emptyQuestions = _samples.Count(s => string.IsNullOrWhiteSpace(s.Question));
        var emptyAnswers = _samples.Count(s => string.IsNullOrWhiteSpace(s.ExpectedAnswer));

        if (emptyQuestions > 0)
        {
            errors.Add($"{emptyQuestions} samples have empty questions.");
        }

        if (emptyAnswers > 0)
        {
            errors.Add($"{emptyAnswers} samples have empty expected answers.");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Merges another dataset into this one.
    /// </summary>
    public void Merge(EvaluationDataset other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var sample in other.Samples)
        {
            if (!_samples.Any(s => s.Id == sample.Id))
            {
                _samples.Add(sample);
            }
        }

        SourceType = "combined";
    }

    /// <summary>
    /// Serializes dataset to JSON.
    /// </summary>
    public string ToJson()
    {
        var dto = new DatasetDto
        {
            Name = Name,
            Description = Description,
            Version = Version,
            SourceType = SourceType,
            CreatedAt = CreatedAt,
            Samples = _samples.Select(s => new SampleDto
            {
                Id = s.Id,
                Question = s.Question,
                ExpectedAnswer = s.ExpectedAnswer,
                Source = s.Source,
                SourcePage = s.SourcePage,
                Section = s.Section,
                Difficulty = s.Difficulty,
                Category = s.Category,
                GameId = s.GameId,
                ExpectedKeywords = s.ExpectedKeywords.ToList(),
                RelevantChunkIds = s.RelevantChunkIds.ToList(),
                DatasetSource = s.DatasetSource
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    /// <summary>
    /// Deserializes dataset from JSON.
    /// </summary>
    public static EvaluationDataset FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<DatasetDto>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) ?? throw new InvalidOperationException("Failed to deserialize dataset JSON.");

        var dataset = new EvaluationDataset
        {
            Name = dto.Name ?? "Unknown",
            Description = dto.Description ?? "",
            Version = dto.Version ?? "1.0.0",
            SourceType = dto.SourceType ?? "unknown",
            CreatedAt = dto.CreatedAt
        };

        foreach (var sampleDto in dto.Samples ?? [])
        {
            var sample = new EvaluationSample
            {
                Id = sampleDto.Id ?? Guid.NewGuid().ToString(),
                Question = sampleDto.Question ?? "",
                ExpectedAnswer = sampleDto.ExpectedAnswer ?? "",
                Source = sampleDto.Source,
                SourcePage = sampleDto.SourcePage,
                Section = sampleDto.Section,
                Difficulty = sampleDto.Difficulty ?? "medium",
                Category = sampleDto.Category ?? "gameplay",
                GameId = sampleDto.GameId,
                ExpectedKeywords = sampleDto.ExpectedKeywords ?? [],
                RelevantChunkIds = sampleDto.RelevantChunkIds ?? [],
                DatasetSource = sampleDto.DatasetSource ?? "unknown"
            };
            dataset._samples.Add(sample);
        }

        return dataset;
    }

    private sealed class DatasetDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? SourceType { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SampleDto>? Samples { get; set; }
    }

    private sealed class SampleDto
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public string? ExpectedAnswer { get; set; }
        public string? Source { get; set; }
        public int? SourcePage { get; set; }
        public string? Section { get; set; }
        public string? Difficulty { get; set; }
        public string? Category { get; set; }
        public string? GameId { get; set; }
        public List<string>? ExpectedKeywords { get; set; }
        public List<string>? RelevantChunkIds { get; set; }
        public string? DatasetSource { get; set; }
    }
}
