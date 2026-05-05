namespace Api.BoundedContexts.AgentMemory.Application.DTOs;

/// <summary>
/// DTO representing a single game glossary entry.
/// </summary>
internal record GlossaryEntryDto(
    string Term,
    string Definition,
    string Language,
    string Source,
    DateTime AddedAt);
