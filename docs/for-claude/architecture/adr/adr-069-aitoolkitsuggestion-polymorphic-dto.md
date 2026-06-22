# ADR-069 — AiToolkitSuggestion Polymorphic DTO Shape + Versioning Strategy

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 2 — US-INT-4 (AI toolkit generation per game)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · issue #1896 (ScoreType polymorphic dispatch, asse A) · B19-3a/B19-3b (v2 additive fields, 2026-05-31)

## Context

`AiToolkitSuggestionDto` (`apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionDtos.cs`) is the structured LLM response parsed by `GenerateToolkitFromKbHandler` via `ILlmService.GenerateJsonAsync<AiToolkitSuggestionDto>()`. The DTO is currently a flat `internal record` with seven tool-list fields and several handler-populated metrics fields.

A v2 expansion (B19-3a, 2026-05-31) added `AiTurnTemplateSuggestion.Rounds`, `TurnsPerRound`, `TurnActions`, `Direction` as **optional/nullable additions** alongside the existing `Phases[]` — backward-compat preserved by default-null values. Similarly, B19-3b added `AiScoringTemplateSuggestion.Categories` (array of `AiScoringCategorySuggestion`) alongside the existing `Dimensions[]`. Both expansions followed an additive-only pattern: new nullable fields that the LLM may or may not populate, deserialized safely when absent.

The `ScoreType` enum (`GameToolkit/Domain/Enums/ScoreType.cs`) currently drives polymorphic score rendering on the frontend: `Points | Ranking | BinaryWin | Objectives` (asse A, issue #1896). The frontend `PolymorphicScoreEditor` (PR #1896, `apps/web/src/`) dispatches to one of four editor components based on a `ScoreChangePayload` discriminated union with `kind` field. This is the closest existing example of polymorphic DTO design in the project.

US-INT-4 requires the backend to expose `AiToolkitSuggestionDto` (or a derived public-facing shape) through an API endpoint so the frontend can render game-specific toolkit suggestions before the user applies them. Currently, `AiToolkitSuggestionDto` is `internal` — it never crosses the HTTP boundary; the handler applies it immediately via `ApplyAiToolkitSuggestionHandler`. To present suggestions to the user for review, a public DTO shape is required.

**Key constraints**:
- `AiToolkitSuggestionDto` is `internal` and is used as a transient LLM parse target, not a DB-persisted entity. Any public API shape is a projection.
- The LLM prompt (`ToolkitExtractionPrompts.cs:90`) explicitly uses string literals `"Points"|"Ranking"|"BinaryWin"|"Objectives"` for `ScoreType` — the LLM output is strongly typed to the enum.
- `AiScoringCategorySuggestion` already introduces a `ScoringComputation` enum (`Count|Sum|RankBased|Custom`) per-category — the scoring section is already more polymorphic than the turn section.
- `ApplyAiToolkitSuggestionCommand` (`GameToolkit/Application/Commands/ApplyAiToolkitSuggestionCommand.cs`) takes the suggestion DTO and converts it to domain entities via `ApplyAiToolkitSuggestionHandler`. The domain side is not affected by this ADR.
- System.Text.Json polymorphism (`[JsonPolymorphic]` / `[JsonDerivedType]`) is available in .NET 9 but requires attribute decoration — not currently used anywhere in the codebase for API responses.

## Problem

The specific architectural question: **what shape should the public `AiToolkitSuggestionDto` take for the `/api/v1/games/{gameId}/toolkit/suggestion` response endpoint, and how should the versioning strategy handle additive changes (new tool types, new scoring computations) without breaking existing frontend clients?**

Sub-decisions:
1. **Discriminator strategy**: explicit `$type` (System.Text.Json `[JsonPolymorphic]`) vs `kind: string` flat field vs flat-DTO with `toolType` tag on each suggestion item.
2. **Versioning**: field-additive (nullable extensions, no discriminator on the root object) vs versioned endpoint (`/v2/`) vs `schemaVersion: int` field.
3. **Backward compat boundary**: additive-only changes permitted vs allow rename with migration map.
4. **Client codegen**: one mega-union TypeScript type vs N narrow types per discriminant.

## Options Considered

### Option A — `[JsonPolymorphic]` + `[JsonDerivedType]` on suggestion items

Each tool suggestion sub-type (`AiDiceToolSuggestion`, `AiCounterToolSuggestion`, etc.) becomes a polymorphic base type with a `$type` discriminator. The root `AiToolkitSuggestionDto` becomes a public record with `IReadOnlyList<AiToolSuggestion>` (base) instead of separate lists per tool type.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AiDiceToolSuggestionDto), "dice")]
[JsonDerivedType(typeof(AiCounterToolSuggestionDto), "counter")]
// ...
public abstract record AiToolSuggestionDto { }
```

**Pros**:
- Single strongly-typed list — frontend can iterate `suggestions[]` and dispatch on `$type`.
- Adding a new tool type = new `[JsonDerivedType]` with no breaking change.
- True open-closed extensibility.

**Cons**:
- `$type` is a non-standard JSON property name — System.Text.Json emits it first in the JSON object (as required by the spec). Some TypeScript JSON parsers expect `$type` at the root, not nested. `zod.discriminatedUnion` requires the discriminant to be a known string literal, not `"$type"`.
- Requires changing `AiToolkitSuggestionDto` from 7 typed lists to 1 heterogeneous list — the internal LLM parse target and the public DTO can no longer share the same record.
- LLM prompt currently returns typed arrays (e.g., `"DiceTools": [...]`) — changing to a single polymorphic list requires prompt re-engineering.

**Risks**: High prompt engineering risk. Non-trivial LLM schema change.

**Impact**: ~3 days. Requires LLM prompt change + new public DTO hierarchy + prompt validation.

---

### Option B — Flat public DTO with `toolType: string` tag per item (explicit discriminant, no JSON polymorphism)

The public response DTO is a flat JSON object:
```json
{
  "toolkitName": "Catan",
  "suggestions": [
    { "toolType": "dice", "name": "Resource Die", "diceType": "D6", ... },
    { "toolType": "counter", "name": "Resource Counter", ... }
  ],
  "scoringTemplate": { "scoreType": "Points", "dimensions": [...], "categories": [...] },
  "turnTemplate": { "turnOrderType": "Clockwise", "phases": [...] },
  "confidenceScore": 0.85,
  "requiresHumanReview": false
}

```

The `suggestions[]` array is untyped on the server (`IReadOnlyList<object>`); the frontend TypeScript discriminated union dispatches on `toolType`.

**Pros**:
- No `[JsonPolymorphic]` attribute complexity.
- Frontend union type is straightforward: `type AiToolSuggestion = AiDiceSuggestion | AiCounterSuggestion | ...` with `toolType` discriminant.
- LLM prompt can continue returning typed arrays — the handler projects them to the flat `suggestions[]` list.
- Adding a new tool type adds a new union member — additive.

**Cons**:
- Server loses type safety on `suggestions[]` — the response serialiser cannot validate the shape.
- The projection from typed internal DTO to flat `suggestions[]` is a mapping step in the handler.
- Zod/TypeScript narrowing on `toolType` requires exhaustive union — a new value added by the server but not yet in the client TS type causes silent `unknown` treatment.

**Risks**: Moderate. Missing client-side narrowing on new tool types causes silent rendering gaps, not errors.

**Impact**: ~2 days. New public DTO class + projection in handler.

---

### Option C — Separate typed lists in public DTO, additive nullable versioning (recommended)

The public response DTO mirrors the internal `AiToolkitSuggestionDto` structure (separate typed lists per tool type) but is a distinct public record with `schemaVersion: int`. Additive changes (new optional fields, new suggestion sub-types) increment `schemaVersion` but are backward-compatible at the deserialization layer.

```csharp
public record AiToolkitSuggestionResponseDto(
    int SchemaVersion,            // v1 = 1; increment on additive changes
    string ToolkitName,
    IReadOnlyList<AiDiceToolSuggestionDto> DiceTools,
    IReadOnlyList<AiCounterToolSuggestionDto> CounterTools,
    IReadOnlyList<AiTimerToolSuggestionDto> TimerTools,
    AiScoringSuggestionDto? ScoringTemplate,
    AiTurnSuggestionDto? TurnTemplate,
    float ConfidenceScore,
    bool RequiresHumanReview,
    IReadOnlyList<AiExcludedToolSuggestionDto>? ExcludedTools = null
);
```

The frontend TypeScript type has N narrow types (one per list), each matching their server shape. A new tool category (e.g., `CardTools`) adds a new nullable list field — existing clients ignore the unknown field (TypeScript strict: must explicitly add to type before use).

`SchemaVersion` signals to clients that they may need a type update but does not change deserialization — it is informational, not a routing discriminator.

**Pros**:
- Mirrors the proven internal DTO structure — projection is trivial (field-for-field).
- Frontend TypeScript types are narrow and strongly typed per tool category — no ambiguous union narrowing.
- Adding a new tool type = new nullable list field + `SchemaVersion` bump. No breaking change.
- Matches the additive pattern already established by B19-3a/3b (optional fields with default-null).
- No LLM prompt changes required.
- Consistent with how existing toolkit commands/DTOs are structured in the codebase.

**Cons**:
- If a game has 0 dice tools and 3 counter tools, the response contains multiple empty lists — not ergonomic for a "unified suggestions feed" UI pattern.
- Adding a fundamentally new tool category that has no precedent in existing types requires the frontend to update its rendering switch before the server deploys — co-ordinated deploy.
- `SchemaVersion` is advisory; there is no server-side enforcement of version-specific serialisation formats.

**Risks**: Low. The projection is simple. The type registry is closed (7 tool types are well-defined). New categories are infrequent and require coordinated FE/BE work regardless of strategy.

**Impact**: ~1.5 days. New public DTO record + endpoint + projection mapper.

---

### Option D — Versioned endpoint (`/v1/` vs `/v2/`)

Route-level versioning: the current endpoint is `/api/v1/games/{gameId}/toolkit/suggestion`; a breaking change introduces `/api/v2/`. Clients opt in explicitly.

**Pros**: Clean version separation, no backward-compat pressure within a version.

**Cons**: The codebase currently uses no route-level API versioning — the convention is field-additive backward compat within the same route (observed across all existing endpoints). Introducing `/v2/` would be the first route-versioned endpoint and sets a precedent for the entire API. Out of scope for US-INT-4.

**Impact**: ~4 days + API versioning infrastructure. Excluded from consideration for this ADR.

## Decision

**Adopt Option C**: separate typed lists in the public `AiToolkitSuggestionResponseDto` with `SchemaVersion: int` as an advisory version signal.

**Rationale**: Option C requires the least architectural novelty. It mirrors the internal DTO faithfully (trivial projection), keeps TypeScript types narrow and strongly typed, and uses the additive nullable-field pattern already established by B19-3a/3b. Option A introduces JSON polymorphism (`[JsonPolymorphic]`) that is not used elsewhere in the codebase and requires LLM prompt restructuring — high risk, low payoff for the current 7 fixed tool categories. Option B loses server-side type safety. `SchemaVersion` is a lightweight signal that the frontend can log and ignore until a client update is deployed.

## Consequences

**Positive**:
- Frontend TypeScript types per tool category are narrow — exhaustive switch rendering is straightforward.
- Additive changes (new fields on existing sub-types, new nullable list for a new tool category) require no endpoint versioning and no existing client breakage.
- `SchemaVersion` provides an upgrade signal that can be monitored in telemetry.

**Negative**:
- Multiple empty lists in the response for games with sparse tool sets (e.g., a game with no timer tools still emits `"timerTools": []`). Acceptable — empty arrays are idiomatic JSON.
- A new top-level tool category (e.g., `CardTools`) added to the server requires the frontend to add a rendering case before the server deploys to avoid silent unknown-field passthrough. This is a coordinated deploy constraint, not a breaking change.

**Trade-offs**:
- `SchemaVersion` is advisory and not enforced. A client receiving `SchemaVersion: 2` with an unknown new list field will simply ignore that field in TypeScript strict mode (no `unknown` property access without explicit type update). The risk is silent missing UI — acceptable for a suggestions preview surface.

## Implementation Guidance

1. **New public DTO**: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionResponseDto.cs` (public, distinct from internal `AiToolkitSuggestionDto`). Include `SchemaVersion = 1`.

2. **Projection mapper**: add `ToResponseDto(AiToolkitSuggestionDto internal)` static method or a dedicated mapper class alongside `ToolkitMapper.cs`. The projection is field-for-field.

3. **Endpoint**: `GET /api/v1/games/{gameId}/toolkit/suggestion` → trigger `GenerateToolkitFromKbQuery` (or reuse `GenerateToolkitFromKbCommand` with a query variant). Handler returns `AiToolkitSuggestionResponseDto`.

4. **TypeScript types**: generate or handcraft `AiToolkitSuggestionResponse` type with `schemaVersion: number` and N typed arrays. The scoring sub-type must include `scoreType: 'Points' | 'Ranking' | 'BinaryWin' | 'Objectives'` matching `ScoreType` enum values.

5. **Version bump discipline**: increment `SchemaVersion` constant in the DTO whenever a new nullable field is added. Document in PR description.

6. **`RequiresHumanReview` gate**: if `RequiresHumanReview: true`, the frontend should render a warning banner before allowing the user to apply the suggestion.

## Rollback / Reversibility

The endpoint is additive (new GET route). Rollback = remove the endpoint registration from `GameToolkitRouting.cs`. The internal DTO and `GenerateToolkitFromKbHandler` are unchanged. The public DTO is a projection-only artifact — no DB migration.

## References

- `AiToolkitSuggestionDtos.cs` — `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionDtos.cs`
- `ToolkitExtractionPrompts.cs` (LLM schema) — `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ToolkitExtractionPrompts.cs:90`
- `ApplyAiToolkitSuggestionHandler.cs` — `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ApplyAiToolkitSuggestionHandler.cs`
- `ScoreType` enum — `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/ScoreType.cs`
- `PolymorphicScoreEditor` (frontend ScoreType dispatch pattern) — `apps/web/src/` (issue #1896 asse A)
- B19-3a/3b additive fields — `AiTurnTemplateSuggestion` + `AiScoringTemplateSuggestion` nullable extensions (2026-05-31 commits)
- Memory: `pdf-indexing-domain-event-bypass.md` (entity factory pattern — use factory methods, not raw constructors)
