# StateTemplate Seed Fixtures

> Issue [#1748](https://github.com/meepleAi-app/meepleai-monorepo/issues/1748) (B19-3d)

JSON fixtures che bootstrappano `StateTemplateDefinition` per i top-N giochi della v1
di MeepleAI. Curated manualmente perché lo spike LLM (`claudedocs/2026-05-31-spike-toolkit-ai-generation.md`)
ha mostrato che per giochi complex flavor (Wingspan, Power Grid) l'AI generation
non basta — è OK come bootstrap helper ma non come source of truth.

## Format

Ogni file è un `AiToolkitSuggestionDto` JSON-serialized:

```jsonc
{
  "ToolkitName": "Wingspan",
  "DiceTools": [...],
  "CounterTools": [...],
  "TimerTools": [...],
  "ScoringTemplate": { ..., "Categories": [...] },  // v3 (B19-3b)
  "TurnTemplate": { ..., "Rounds": 4, "TurnsPerRound": [8,7,6,5] },  // v3 (B19-3a)
  "Overrides": { ... 3 boolean ... },
  "Reasoning": "Curated by domain expert YYYY-MM-DD.",
  "ExcludedTools": [...]
}
```

Campi `ConfidenceScore`/`ChunksAnalyzed`/`KbCoveragePercent`/`RequiresHumanReview`
non sono presenti (sono populated dal handler runtime, non statici nel seed).

## Status fixtures

| Game | File | Status | Archetype | Curator |
|---|---|---|---|---|
| Wingspan | `wingspan.json` | ✅ complete | Euro engine-builder | Claude Code (spike-derived) |
| Codenames | `codenames.json` | ✅ complete | Team deduction | Claude Code |
| Paleo | `paleo.json` | ✅ complete | Co-op simultaneous | Claude Code |
| Catan | `catan.json` | ✅ complete | Euro + trading | Claude Code |
| Puerto Rico | `puerto-rico.json` | ⏳ stub TBD | Euro role-selection | Pending domain expert |
| Power Grid | `power-grid.json` | ⏳ stub TBD | Euro + auction | Pending domain expert |
| Zombicide Green Horde | `zombicide-green-horde.json` | ⏳ stub TBD | Co-op miniatures | Pending domain expert |

Stub TBD = fixture con `ToolkitName` + minimal note `Reasoning="Requires manual
curation by domain expert"`. Da completare in iterazione successiva (sub-PR
di #1748 o issue dedicata per gioco).

## Bootstrap via AI fallback

Per i giochi non ancora seeded, il flow è:

1. `GenerateToolkitFromKbCommand(gameId, userId)` (BackEnd: `apps/api/.../GenerateToolkitFromKbHandler.cs`)
2. Output `AiToolkitSuggestionDto` con `RequiresHumanReview=true` se confidence < 0.85
3. Admin review via `ApplyAiToolkitSuggestionCommand` → produce un `GameToolkit` (Draft)
4. Quando il toolkit è "approved" via marketplace workflow, può diventare seed canonical

## Validazione

Ogni JSON deserializza in `AiToolkitSuggestionDto`. Test:

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~StateTemplateFixtureTests"
```

I test caricano ogni file e validano lo schema deserialization.
