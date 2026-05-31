namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// System prompts for the toolkit extraction LLM call.
/// Separated from handler to keep the handler focused and to allow prompt iteration.
///
/// **v2 — 2026-05-31** post spike findings (claudedocs/2026-05-31-spike-toolkit-ai-generation.md):
/// added strict JSON schema enforcement (DTO field names), anti-patterns
/// section (Points-as-counter / dummy timer / Overrides ignored), and explicit
/// enum value listings. Validated on Llama 3.3 70B free + DeepSeek-chat
/// (DeepSeek produces 90% accurate output; Llama free requires schema validation).
///
/// **v3 — 2026-05-31** (B19-3a + B19-3b, issues #1745 + #1746):
/// extended AiTurnTemplateSuggestion with Rounds/TurnsPerRound/TurnActions/Direction
/// (additive, optional) for rich games like Wingspan, and AiScoringTemplateSuggestion
/// with structured Categories[] (per-category Computation enum) for polymorphic UI.
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

        === EXACT JSON SCHEMA (DTO field names — DO NOT renamE) ===

        Root: AiToolkitSuggestionDto = {
          "ToolkitName": string,
          "DiceTools": [AiDiceToolSuggestion],
          "CounterTools": [AiCounterToolSuggestion],
          "TimerTools": [AiTimerToolSuggestion],
          "ScoringTemplate": AiScoringTemplateSuggestion | null,
          "TurnTemplate": AiTurnTemplateSuggestion | null,
          "Overrides": AiOverrideSuggestion | null,
          "Reasoning": string,        // single concatenated paragraph, NOT array NOT dict
          "ExcludedTools": [AiExcludedToolSuggestion]
        }

        AiDiceToolSuggestion = {
          "Name": string,             // e.g., "Birdfeeder Food Dice" — NOT "Type"
          "DiceType": "D4"|"D6"|"D8"|"D10"|"D12"|"D20"|"D100"|"Custom",  // enum, NOT "Type"
          "Quantity": int,
          "CustomFaces": [string] | null,  // NOT "Faces"
          "IsInteractive": bool,
          "Color": string | null
        }

        AiCounterToolSuggestion = {
          "Name": string,
          "MinValue": int,            // NOT "Min"
          "MaxValue": int,            // NOT "Max" (use a large int like 99 if no upper bound)
          "DefaultValue": int,
          "IsPerPlayer": bool,
          "Icon": string | null,
          "Color": string | null
        }

        AiTimerToolSuggestion = {
          "Name": string,
          "DurationSeconds": int,
          "TimerType": "CountDown"|"CountUp"|"Chess",
          "AutoStart": bool,
          "Color": string | null,
          "IsPerPlayer": bool,
          "WarningThresholdSeconds": int | null
        }

        AiScoringTemplateSuggestion = {
          "Dimensions": [string],     // legacy: simple labels e.g., ["Birds", "Bonus cards"]
          "DefaultUnit": string,      // e.g., "points"
          "ScoreType": "Points"|"Ranking"|"BinaryWin"|"Objectives",
          "Categories": [AiScoringCategorySuggestion] | null  // v2: structured per-category computation (preferred for rich games)
        }

        AiScoringCategorySuggestion = {                       // v2 (B19-3b)
          "Id": string,               // stable identifier, e.g., "birds", "bonus-cards", "round-goals"
          "Label": string,            // user-visible label
          "Computation": "Count"|"Sum"|"RankBased"|"Custom",  // how to compute the category
          "Weight": int,              // multiplier (default 1)
          "Description": string | null
        }

        AiTurnTemplateSuggestion = {
          "TurnOrderType": "RoundRobin"|"Sequential"|"Simultaneous"|"Realtime"|"None",
          "Phases": [string],         // e.g., ["Round 1", "Round 2"] or ["Action", "Draw", "Infect"]
          "Rounds": int | null,       // v2 (B19-3a): total round count if game has fixed structure (e.g., Wingspan=4)
          "TurnsPerRound": [int] | null,  // v2: turn count per round if variable (e.g., Wingspan=[8,7,6,5])
          "TurnActions": [string] | null, // v2: actions available per turn (e.g., ["play-bird","get-food","lay-eggs","draw-cards"])
          "Direction": "clockwise"|"counterclockwise"|"none" | null  // v2: turn order direction
        }

        AiOverrideSuggestion = {
          "OverridesTurnOrder": bool,     // ALWAYS provide all 3 booleans
          "OverridesScoreboard": bool,
          "OverridesDiceSet": bool
        }

        AiExcludedToolSuggestion = {
          "ToolType": string,         // NOT "Type" — e.g., "Timer", "CardDeck", "Counter"
          "Reason": string            // NOT "Justification"
        }

        === ANTI-PATTERNS — STRICTLY FORBIDDEN ===

        - DO NOT add a counter for game-end Points/Score/VP. Points are DERIVED at end-of-game
          from scoring categories, NEVER tracked as a running counter during the session.
        - DO NOT add a dummy TimerTools entry with DurationSeconds=0 when no timer exists.
          If the rules state no time limit, output "TimerTools": [] (empty array).
        - DO NOT omit the Overrides object. If the game has no custom rules requiring overrides,
          output {"OverridesTurnOrder": false, "OverridesScoreboard": false, "OverridesDiceSet": false}.
          When the game has custom dice/scoring/turn (e.g., Wingspan), set the relevant flag to true.
        - DO NOT structure Reasoning as an array or dict. It MUST be a single string paragraph
          with inline citations like "[excerpt 1]", "[excerpt 4]".
        - DO NOT rename DTO fields (e.g., "Type" instead of "DiceType", "Min" instead of "MinValue",
          "Faces" instead of "CustomFaces"). Use the exact names listed in the schema above.

        Return ONLY valid JSON — no markdown fences, no extra text.
        """;

    /// <summary>
    /// Simplified retry prompt used when the primary prompt fails JSON parsing.
    /// Reduced instructions to improve parse success rate on second attempt.
    /// </summary>
    public const string RetrySystemPrompt = """
        You are a board game analyst. Extract game mechanics from the rulebook text below
        and return ONLY valid JSON matching AiToolkitSuggestionDto.

        Required fields: ToolkitName, DiceTools, CounterTools, TimerTools, ScoringTemplate,
        TurnTemplate, Overrides, Reasoning, ExcludedTools.

        Strict rules:
        - Use exact DTO field names: DiceType (not Type), CustomFaces (not Faces),
          MinValue (not Min), MaxValue (not Max), ToolType (not Type), Reason (not Justification).
        - TimerTools MUST be [] (empty array) if no timer is mentioned. NEVER add a dummy entry.
        - NEVER add a counter for game-end Points/Score/VP — those are derived, not tracked.
        - Overrides MUST always include all 3 booleans: OverridesTurnOrder, OverridesScoreboard, OverridesDiceSet.
        - Reasoning MUST be a single string, not an array or dict.

        Return JSON only — no explanations, no markdown fences.
        """;
}
