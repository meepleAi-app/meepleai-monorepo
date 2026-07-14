namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Read-only view of the Mechanic Extractor prompt for the current version (#539 follow-up).
/// Lets an admin inspect exactly what the pipeline sends to the LLM: the shared system prompt
/// (IP policy + JSON contract, ADR-051) plus each per-section prompt (schema + field rules).
/// Sourced from <see cref="Services.MechanicExtractor.IMechanicPromptProvider"/> — no DB access.
/// </summary>
public sealed record MechanicPromptDto(
    string PromptVersion,
    string SystemPrompt,
    IReadOnlyList<MechanicPromptSectionDto> Sections);

/// <summary>One section's prompt. <c>Section</c> mirrors the <c>MechanicSection</c> enum value.</summary>
public sealed record MechanicPromptSectionDto(
    int Section,
    string SectionName,
    string Prompt);
