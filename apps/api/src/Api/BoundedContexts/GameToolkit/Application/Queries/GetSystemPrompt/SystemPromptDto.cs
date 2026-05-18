namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetSystemPrompt;

/// <summary>
/// Owner-only system-prompt projection: includes the full prompt text and
/// metadata needed by the author's debug / transparency surface
/// (issue #822 / spec-panel 2026-05-18 §2 — Wiegers DTO shape).
/// </summary>
/// <param name="FullPrompt">
/// The complete system prompt string as authored. May be empty when the
/// toolkit has no <c>AgentConfig.systemPrompt</c> set yet.
/// </param>
/// <param name="AgentMode">
/// The agent's persona configuration slot key from <c>GameToolkit.AgentConfig</c>
/// JSON (the <c>mode</c> or <c>agentMode</c> field — "Marco's mode" naming
/// from PR #732 §5.3.1 mockup variants). Falls back to <c>"default"</c>
/// when the field is missing.
/// </param>
/// <param name="GeneratedAt">
/// UTC timestamp when this projection was rendered. Owners use this to
/// correlate debug sessions against the cached 10-min window.
/// </param>
internal sealed record SystemPromptOwnerDto(
    string FullPrompt,
    string AgentMode,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Public viewer projection: returns only the agent mode plus the prompt's
/// character count so non-authors can see "this toolkit ships a non-trivial
/// agent" without leaking the prompt body
/// (issue #822 / spec-panel 2026-05-18 §2 — Wiegers DTO shape).
/// </summary>
internal sealed record SystemPromptPublicDto(
    string AgentMode,
    int CharacterCount);

/// <summary>
/// Discriminated envelope returned by the query — exactly one of the two
/// nested DTOs is non-null. The endpoint serialises whichever is set.
/// </summary>
internal sealed record SystemPromptResponse(
    SystemPromptOwnerDto? Owner,
    SystemPromptPublicDto? Public);
