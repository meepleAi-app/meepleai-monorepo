namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>The write that found the prompt slot already taken.</summary>
internal enum PromptSlotWrite
{
    /// <summary>A new analysis with <c>ForceRegenerate</c> (#4144).</summary>
    Regenerate,

    /// <summary>A Rejected analysis resubmitted for review (Rejected → InReview re-enters the index).</summary>
    ResubmitForReview,
}
