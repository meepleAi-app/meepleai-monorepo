# Seed StateTemplate JSON — Preview da PR #1761

> ⚠️ **Cartella temporanea / preview-only** — questi 7 JSON sono **estratti
> dal branch `feature/issue-1748-seed-state-templates`** (PR #1761 ancora
> OPEN, non mergiato in `main-dev` al momento della creazione di questa
> cartella, 2026-05-31).

## Scopo

Servono come **fixture** da allegare alle chat Claude Design Web durante
la creazione dei mockup (skeleton + 6 game premium), per testare
visivamente che il rendering polimorfico funzioni con giochi diversi.

## Dopo merge PR #1761

Quando #1761 mergia in `main-dev`, la cartella canonica diventa:
`apps/api/src/Api/BoundedContexts/GameToolkit/Seed/StateTemplates/`

Questa cartella `claudedocs/seed-preview-from-pr-1761/` può essere
**rimossa** (sono duplicati). Per ora resta per workflow Claude Design Web.

## Status dei 7 JSON

| Game | File | Completezza |
|---|---|---|
| Wingspan | `wingspan.json` | ✅ complete (engine-builder 4-round) |
| Codenames | `codenames.json` | ✅ complete (team deduction) |
| Paleo | `paleo.json` | ✅ complete (co-op simultaneous) |
| Catan | `catan.json` | ✅ complete (euro + 2D6) |
| Puerto Rico | `puerto-rico.json` | ⏳ stub TBD (richiede curator) |
| Power Grid | `power-grid.json` | ⏳ stub TBD (4-phase auction) |
| Zombicide GH | `zombicide-green-horde.json` | ⏳ stub TBD (co-op miniatures) |

Per il mockup skeleton, usa **Wingspan + Paleo** (entrambi complete + coprono
2 archetypi distinti: euro engine vs co-op simultaneous).

Cross-reference: `claudedocs/2026-05-31-spike-toolkit-ai-generation.md`
documenta il prompt + DTO schema che ha prodotto questi fixture.
