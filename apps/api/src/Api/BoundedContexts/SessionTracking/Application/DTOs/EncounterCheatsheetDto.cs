namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// Encounter cheatsheet payload extracted from a gamebook photo segment.
///
/// Issue #1520 (Phase A runthrough, N3 + §9.1): unlike the narrative storybook
/// translation pipeline (<see cref="TranslateChunk"/>), encounter parses are
/// <b>ephemeral</b> — the result is not persisted. The card is meant to live
/// only for the duration of the active <c>GamebookCampaignSession</c> resolution
/// flow ("Apri Encounter Book" → cheatsheet rendered → outcome banner).
///
/// Structure mirrors the canonical mockup
/// (<c>admin-mockups/design_files/librogame-runthrough-encounter-cheatsheet.html</c>),
/// state C "cheatsheet-rendered": enemy header + stats grid (HP/ATK/DEF/MOV) +
/// options list with optional inline dice rolls + win/loss conditions.
///
/// All stat values are strings so the LLM extractor can preserve raw textual
/// tokens (e.g. "5+1", "—", "see §220") without forcing a brittle int parse;
/// the FE renders them verbatim.
/// </summary>
public sealed record EncounterCheatsheetDto(
    IReadOnlyList<EncounterEnemyDto> Enemies,
    IReadOnlyList<EncounterOptionDto> Options,
    EncounterConditionsDto Conditions,
    EncounterConfidenceDto Confidence);

/// <summary>
/// A single hostile/encounter actor with a fixed 4-slot stat block. The
/// HP/ATK/DEF/MOV slots match the mockup grid; absent stats are represented
/// as a literal em-dash "—" (consumer renders verbatim).
/// </summary>
public sealed record EncounterEnemyDto(
    string Name,
    string? Icon,
    string? ParagraphMarker,
    string Hp,
    string Atk,
    string Def,
    string Mov);

/// <summary>
/// A player-facing choice in the encounter. <see cref="DiceRoll"/> is optional
/// — narrative choices (no roll required) leave it null.
/// </summary>
public sealed record EncounterOptionDto(
    string Label,
    EncounterDiceRollDto? DiceRoll,
    string? Outcome);

/// <summary>
/// Inline dice roll specification for an option, e.g. "1d6+1 vs ≥ 4".
/// </summary>
public sealed record EncounterDiceRollDto(
    int Sides,
    int Count,
    int Modifier,
    int Threshold);

/// <summary>
/// Outcome conditions split into win/loss strings as shown by the mockup
/// "conditions split-card". Either side may be null if the encounter cannot
/// fail outright (e.g. narrative-only branching).
/// </summary>
public sealed record EncounterConditionsDto(
    string? Win,
    string? Loss);

/// <summary>
/// Per-cluster parse confidence in [0, 1]. The mockup uses this to badge each
/// section and to gate manual-input fallback (per §9.1: parse confidence per
/// campo &lt; 0.6 = forced manual input).
/// </summary>
public sealed record EncounterConfidenceDto(
    double Enemies,
    double Options,
    double Conditions);
