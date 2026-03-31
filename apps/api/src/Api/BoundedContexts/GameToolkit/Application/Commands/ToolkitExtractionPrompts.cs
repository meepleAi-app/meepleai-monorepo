namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// System prompts for the toolkit extraction LLM call.
/// Separated from handler to keep the handler focused and to allow prompt iteration.
/// </summary>
internal static class ToolkitExtractionPrompts
{
    /// <summary>
    /// Primary system prompt instructing the LLM to extract game mechanics as structured JSON.
    /// </summary>
    public const string SystemPrompt = """
        You are an expert board game rules analyst. Analyze the provided rulebook excerpts
        and extract mechanical components needed to configure a digital game toolkit.

        For each component type, output structured JSON. Include ONLY components explicitly
        mentioned or strongly implied by the rules provided. Do not invent components.

        DICE: Extract dice types (D4/D6/D8/D10/D12/D20/D100/Custom), quantities, custom faces.
        COUNTERS: Extract resources, tokens, points (min/max values, IsPerPlayer=true if tracked per player).
        TIMERS: Extract time limits, turn timers (DurationSeconds, AutoStart, IsPerPlayer).
        SCORING: Extract victory conditions, points dimensions, ranking vs points system.
        TURN ORDER: Extract round structure (RoundRobin/Sequential) and phase names if present.
        EXCLUDED: List tool types NOT needed with a brief rule-based justification.

        Rules:
        - Set IsPerPlayer=true for anything tracked individually (e.g., player resources, player timers)
        - Use min=0 for counters unless rules specify negative values
        - DurationSeconds=0 if no explicit time limit is mentioned
        - Reasoning must cite the rule text that led to each decision
        - Return ONLY valid JSON — no markdown fences, no extra text

        JSON schema: AiToolkitSuggestionDto with fields: ToolkitName, DiceTools, CounterTools,
        TimerTools, ScoringTemplate, TurnTemplate, Overrides, Reasoning, ExcludedTools.
        """;

    /// <summary>
    /// Simplified retry prompt used when the primary prompt fails JSON parsing.
    /// Reduced instructions to improve parse success rate on second attempt.
    /// </summary>
    public const string RetrySystemPrompt = """
        You are a board game analyst. Extract game mechanics from the rulebook text below
        and return ONLY valid JSON matching AiToolkitSuggestionDto.
        Include: ToolkitName, DiceTools, CounterTools, TimerTools,
        ScoringTemplate, TurnTemplate, Overrides, Reasoning.
        ExcludedTools can be an empty array.
        Return JSON only — no explanations.
        """;
}
