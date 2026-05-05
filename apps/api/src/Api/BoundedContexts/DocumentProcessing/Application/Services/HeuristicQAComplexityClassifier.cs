namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Keyword-heuristic implementation. Sync, allocation-efficient, Singleton-safe.
/// Extends QueryComplexityAnalyzer pattern for Q&amp;A-specific classification.
/// </summary>
internal sealed class HeuristicQAComplexityClassifier : IQAComplexityClassifier
{
    private static readonly string[] SynthesisKeywords =
        ["differenza", "difference", "confronta", "compare", "versus", "vs",
         "meglio", "better", "ottimale", "optimal", "confronto", "comparison"];

    private static readonly string[] MultiStepKeywords =
        ["se poi", "if then", "after", "before", "quando", "durante",
         "combina", "combine", "insieme", "together", "e poi", "and then"];

    private static readonly string[] SimplePatterns =
        ["quanti", "quanto", "how many", "what is", "cosa è", "dove", "where",
         "quando", "when", "chi", "who", "qual è", "which"];

    public QAComplexityResult Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new QAComplexityResult(QAComplexityLevel.Simple, 1.0f, "empty_input");

        var lower = question.ToLowerInvariant();

        if (SynthesisKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.Synthesis, 0.85f, "synthesis_keyword");

        if (MultiStepKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.MultiStep, 0.80f, "multistep_keyword");

        if (SimplePatterns.Any(k => lower.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
            return new QAComplexityResult(QAComplexityLevel.Simple, 0.90f, "simple_prefix");

        return new QAComplexityResult(QAComplexityLevel.MultiStep, 0.60f, "default_multistep");
    }
}
