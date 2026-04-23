using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Loads Mechanic Extractor prompts (system + per-section user instructions) from embedded
/// resources. ISSUE-524 / M1.2 — decision B4=A: prompts live as <c>.md</c> files checked
/// into git and compiled as <c>&lt;EmbeddedResource&gt;</c>.
/// </summary>
public interface IMechanicPromptProvider
{
    /// <summary>Canonical prompt version, e.g. <c>"v1.0.0"</c>.</summary>
    string PromptVersion { get; }

    /// <summary>Returns the shared system prompt (IP policy).</summary>
    string GetSystemPrompt();

    /// <summary>Returns the user prompt for the given section.</summary>
    string GetSectionPrompt(MechanicSection section);
}
