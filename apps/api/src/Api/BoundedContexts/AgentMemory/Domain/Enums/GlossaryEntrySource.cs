namespace Api.BoundedContexts.AgentMemory.Domain.Enums;

/// <summary>
/// Indicates the origin of a glossary entry.
/// </summary>
internal enum GlossaryEntrySource
{
    /// <summary>Manually added by a user.</summary>
    Manual,

    /// <summary>Defined by the user (alias for Manual, kept for API compatibility).</summary>
    UserDefined,

    /// <summary>Auto-bootstrapped by NER pipeline (Phase 3).</summary>
    NerBootstrap
}
