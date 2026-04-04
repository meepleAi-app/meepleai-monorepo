namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

public enum QueryComplexityLevel { Simple = 0, Moderate = 1, Complex = 2 }

internal sealed record QueryComplexity(QueryComplexityLevel Level, float Confidence, string Reason)
{
    public bool RequiresRetrieval => Level != QueryComplexityLevel.Simple;
    public bool RequiresMultiStep => Level == QueryComplexityLevel.Complex;
    public bool CanDowngradeToFast => Level == QueryComplexityLevel.Simple;

    public static QueryComplexity Simple(string reason, float confidence) =>
        new(QueryComplexityLevel.Simple, confidence, reason);

    public static QueryComplexity Moderate(string reason, float confidence) =>
        new(QueryComplexityLevel.Moderate, confidence, reason);

    public static QueryComplexity Complex(string reason, float confidence) =>
        new(QueryComplexityLevel.Complex, confidence, reason);
}
