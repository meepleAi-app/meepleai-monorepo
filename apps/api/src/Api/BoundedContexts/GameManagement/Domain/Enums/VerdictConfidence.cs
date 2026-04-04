namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// How confident the AI arbitrator is in its ruling.
/// </summary>
internal enum VerdictConfidence
{
    /// <summary>High confidence — clear rule text supports the verdict.</summary>
    High = 0,

    /// <summary>Medium confidence — rule text is somewhat supportive but not definitive.</summary>
    Medium = 1,

    /// <summary>Low confidence — ruling is largely interpretive.</summary>
    Low = 2
}
