namespace Api.SharedKernel.Domain.Enums;

/// <summary>
/// Grounding status of an AI agent answer, shared across bounded contexts.
/// Derived from citations: <see cref="Grounded"/> iff the answer carries at
/// least one citation and is not the refusal ("not available in the rulebook",
/// #4481), otherwise <see cref="Ungrounded"/>. Producers with an answer text use
/// <c>AnswerGrounding</c> (KnowledgeBase). <see cref="Partial"/>
/// is reserved for future use (#3390) and has no producer yet.
/// </summary>
public enum GroundingStatus
{
    Grounded = 0,
    Partial = 1,
    Ungrounded = 2
}
