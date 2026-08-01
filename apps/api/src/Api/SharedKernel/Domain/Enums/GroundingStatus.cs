namespace Api.SharedKernel.Domain.Enums;

/// <summary>
/// Grounding status of an AI agent answer, shared across bounded contexts.
/// Derived from citations: <see cref="Grounded"/> iff the answer carries at
/// least one citation, otherwise <see cref="Ungrounded"/>. <see cref="Partial"/>
/// is reserved for future use (#3390) and has no producer yet.
/// </summary>
public enum GroundingStatus
{
    Grounded = 0,
    Partial = 1,
    Ungrounded = 2
}
