namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing validation accuracy metrics for the multi-layer validation system.
/// BGAI-039: Measures how accurately the validation system identifies correct vs. incorrect responses.
/// Target: >= 80% accuracy baseline
/// </summary>
public record ValidationAccuracyMetrics
{
    /// <summary>
    /// True Positives: Valid responses correctly identified as valid
    /// </summary>
    public int TruePositives { get; init; }

    /// <summary>
    /// True Negatives: Invalid responses correctly identified as invalid
    /// </summary>
    public int TrueNegatives { get; init; }

    /// <summary>
    /// False Positives: Invalid responses incorrectly identified as valid (Type I error)
    /// </summary>
    public int FalsePositives { get; init; }

    /// <summary>
    /// False Negatives: Valid responses incorrectly identified as invalid (Type II error)
    /// </summary>
    public int FalseNegatives { get; init; }

    /// <summary>
    /// Total number of validation cases
    /// </summary>
    public int Total => TruePositives + TrueNegatives + FalsePositives + FalseNegatives;

    /// <summary>
    /// Precision: What percentage of "valid" predictions are actually valid?
    /// Formula: TP / (TP + FP)
    /// Range: 0.0 to 1.0
    /// </summary>
    public double Precision
    {
        get
        {
            var denominator = TruePositives + FalsePositives;
            return denominator > 0 ? (double)TruePositives / denominator : 0.0;
        }
    }

    /// <summary>
    /// Recall (Sensitivity): What percentage of actually valid cases are correctly identified?
    /// Formula: TP / (TP + FN)
    /// Range: 0.0 to 1.0
    /// </summary>
    public double Recall
    {
        get
        {
            var denominator = TruePositives + FalseNegatives;
            return denominator > 0 ? (double)TruePositives / denominator : 0.0;
        }
    }

    /// <summary>
    /// F1-Score: Harmonic mean of precision and recall
    /// Formula: 2 * (Precision * Recall) / (Precision + Recall)
    /// Range: 0.0 to 1.0
    /// Best when precision and recall are balanced
    /// </summary>
    public double F1Score
    {
        get
        {
            var sum = Precision + Recall;
            return sum > 0 ? 2.0 * (Precision * Recall) / sum : 0.0;
        }
    }

    /// <summary>
    /// Accuracy: Overall correctness rate
    /// Formula: (TP + TN) / (TP + TN + FP + FN)
    /// Range: 0.0 to 1.0
    /// Target: >= 0.80 (80%+)
    /// </summary>
    public double Accuracy
    {
        get
        {
            return Total > 0 ? (double)(TruePositives + TrueNegatives) / Total : 0.0;
        }
    }

    /// <summary>
    /// Specificity: What percentage of actually invalid cases are correctly identified?
    /// Formula: TN / (TN + FP)
    /// Range: 0.0 to 1.0
    /// </summary>
    public double Specificity
    {
        get
        {
            var denominator = TrueNegatives + FalsePositives;
            return denominator > 0 ? (double)TrueNegatives / denominator : 0.0;
        }
    }

    /// <summary>
    /// Matthews Correlation Coefficient: Balanced measure even with imbalanced classes
    /// Formula: (TP*TN - FP*FN) / sqrt((TP+FP)(TP+FN)(TN+FP)(TN+FN))
    /// Range: -1.0 to 1.0 (1.0 = perfect, 0.0 = random, -1.0 = perfect disagreement)
    /// </summary>
    public double MatthewsCorrelationCoefficient
    {
        get
        {
            var numerator = (TruePositives * TrueNegatives) - (FalsePositives * FalseNegatives);
            var denominator = Math.Sqrt(
                (TruePositives + FalsePositives) *
                (TruePositives + FalseNegatives) *
                (TrueNegatives + FalsePositives) *
                (TrueNegatives + FalseNegatives));

            return denominator > 0 ? numerator / denominator : 0.0;
        }
    }

    /// <summary>
    /// Checks if validation accuracy meets the minimum threshold (>= 80%)
    /// </summary>
    public bool MeetsBaselineThreshold => Accuracy >= 0.80;

    /// <summary>
    /// Gets the quality level based on accuracy
    /// </summary>
    public ValidationAccuracyLevel QualityLevel
    {
        get
        {
            return Accuracy switch
            {
                >= 0.95 => ValidationAccuracyLevel.Excellent,
                >= 0.90 => ValidationAccuracyLevel.VeryGood,
                >= 0.80 => ValidationAccuracyLevel.Good,
                >= 0.70 => ValidationAccuracyLevel.Fair,
                >= 0.60 => ValidationAccuracyLevel.Poor,
                _ => ValidationAccuracyLevel.Critical
            };
        }
    }

    /// <summary>
    /// Creates a new ValidationAccuracyMetrics instance with the specified confusion matrix values
    /// </summary>
    public static ValidationAccuracyMetrics Create(int truePositives, int trueNegatives, int falsePositives, int falseNegatives)
    {
        if (truePositives < 0) throw new ArgumentException("True positives cannot be negative", nameof(truePositives));
        if (trueNegatives < 0) throw new ArgumentException("True negatives cannot be negative", nameof(trueNegatives));
        if (falsePositives < 0) throw new ArgumentException("False positives cannot be negative", nameof(falsePositives));
        if (falseNegatives < 0) throw new ArgumentException("False negatives cannot be negative", nameof(falseNegatives));

        return new ValidationAccuracyMetrics
        {
            TruePositives = truePositives,
            TrueNegatives = trueNegatives,
            FalsePositives = falsePositives,
            FalseNegatives = falseNegatives
        };
    }

    /// <summary>
    /// Creates empty metrics (all zeros)
    /// </summary>
    public static ValidationAccuracyMetrics Empty => Create(0, 0, 0, 0);
}

/// <summary>
/// Quality level classification for validation accuracy
/// </summary>
public enum ValidationAccuracyLevel
{
    /// <summary>Critical: Accuracy < 60% - System unreliable</summary>
    Critical = 0,

    /// <summary>Poor: Accuracy 60-69% - Needs improvement</summary>
    Poor = 1,

    /// <summary>Fair: Accuracy 70-79% - Below target</summary>
    Fair = 2,

    /// <summary>Good: Accuracy 80-89% - Meets baseline (target)</summary>
    Good = 3,

    /// <summary>Very Good: Accuracy 90-94% - Exceeds expectations</summary>
    VeryGood = 4,

    /// <summary>Excellent: Accuracy >= 95% - Outstanding performance</summary>
    Excellent = 5
}
