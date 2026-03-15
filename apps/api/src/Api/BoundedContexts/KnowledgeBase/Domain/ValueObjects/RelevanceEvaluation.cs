namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

public enum RelevanceVerdict { Correct = 0, Ambiguous = 1, Incorrect = 2 }

internal sealed record RelevanceEvaluation(RelevanceVerdict Verdict, float Confidence, string Reason)
{
    public bool UseRetrievedDocuments => Verdict != RelevanceVerdict.Incorrect;
    public bool ShouldRequery => Verdict != RelevanceVerdict.Correct;
}
