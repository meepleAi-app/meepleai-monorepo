namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Copyright protection tier for RAG citations.
/// Determines whether citations are shown verbatim (Full) or paraphrased (Protected).
/// </summary>
public enum CopyrightTier
{
    /// <summary>Protected by default — safe fallback. AI paraphrases the content.</summary>
    Protected = 0,

    /// <summary>Full access — verbatim citation with PDF viewer access.</summary>
    Full = 1
}
