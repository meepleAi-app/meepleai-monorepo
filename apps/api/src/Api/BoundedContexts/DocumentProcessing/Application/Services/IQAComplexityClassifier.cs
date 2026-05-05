namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal enum QAComplexityLevel { Simple, MultiStep, Synthesis }

internal sealed record QAComplexityResult(
    QAComplexityLevel Level,
    float Confidence,
    string Reason);

/// <summary>
/// Classifies Q&amp;A answering complexity for photo-batch book assistant context.
/// Distinct from IQueryComplexityClassifier (RAG routing) and QueryComplexityAnalyzer (model routing).
/// Used to select chain-of-thought prompting and model tier for Q&amp;A over photo-indexed content.
/// </summary>
internal interface IQAComplexityClassifier
{
    QAComplexityResult Classify(string question);
}
