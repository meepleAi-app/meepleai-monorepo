# Wire real turnOrderType into TurnIndicatorRenderer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** Sostituire il placeholder hardcoded `type: 'RoundRobin'` in `SessionLiveView` con il `turnOrderType` reale, alimentando `TurnIndicatorRenderer` (G5b, già fatto) dai dati della session.

**Architecture:** BE source = **B (derive dal toolkit) con fallback ad A (field Session + migration + SignalR event)** — deciso in Task 1. FE: store field + adapter + view wiring. Replica il pattern G5a (scoringType, #2389/#2430).

**Tech Stack:** .NET 9 (SessionTracking BC, EF Core, SignalR `GameStateHub`) · Next.js 16 · Zustand (`useLiveSessionStore`) · Vitest · xUnit.

**Spec:** `docs/superpowers/specs/2026-06-22-issue-2483-turnordertype-wiring-design.md`

---

## Template G5a (scoringType) — riferimenti file:line (l'esecutore li legge)
- BE DTO: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionDto.cs:21,27` (`ScoringType`/`ScoreData`)
- BE query handler: `…/Application/Queries/GetActiveSessionQueryHandler.cs:62-76` (costruisce il DTO)
- BE event+handler+hub (path A): `…/Domain/Events/SessionScoresUpdatedEvent.cs`, `…/Application/EventHandlers/SessionScoresUpdatedSignalRHandler.cs`, `Api/Hubs/GameStateHub.cs:317-325`
- BE Session aggregate: `…/Domain/Entities/Session.cs:112,453-463` (`ScoringType` + `SetScores`)
- FE store: `apps/web/src/lib/stores/live-session-store.ts:59-81` (`scoringType` + `setScoringConfig`)
- FE adapter: `apps/web/src/lib/session-live/score-data-to-panel-data.ts:35-108` (`mapScoreDataToPanelData`)
- FE SignalR (path A): `apps/web/src/lib/domain-hooks/useSignalrSession.ts:49-143` (`'ScoringConfigured'`)
- FE view: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` (scoringPanelData → `ScoringPanelRenderer`; turnRendererState hardcoded `:606-615`)
- FE types: `apps/web/src/lib/session-live/turn-state.ts:12-58` (`TurnOrderType` union + `TurnState`)
- BE enum: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/TurnOrderType.cs:10-20` (7 values)

---

### Task 1 (BE): decidi B vs A ed esponi `turnOrderType` nel session DTO

**Files:**
- Read: `GetActiveSessionQueryHandler.cs`, `Session.cs`, `Game` aggregate + `GameToolkit` aggregate (cerca la FK `Game → toolkit` e `GameToolkit.TurnOrderType`), `SessionDto.cs`
- Modify: `SessionDto.cs` (+ `string? TurnOrderType`), `GetActiveSessionQueryHandler.cs`
- (path A only) Create: `SessionTurnOrderTypeUpdatedEvent.cs` + `SessionTurnOrderTypeUpdatedSignalRHandler.cs`; Modify `Session.cs` (+ field + `SetTurnOrderType`) + `GameStateHub.cs` + EF migration
- Test: `GetActiveSessionQueryHandlerTests` (+ handler test if A)

- [ ] **Step 1 — Decidi B vs A.** Verifica se il query handler può risolvere `GameToolkit.TurnOrderType` per il game della session (catena `Session.GameId → Game → GameToolkit.TurnOrderType`). Cerca la relazione Game↔GameToolkit. **Se accessibile pulito → B** (deriva nel handler, no nuovo field/migration/event). **Se no → A** (field `Session.TurnOrderType` + migration + `SetTurnOrderType` + event + SignalR handler + hub, replicando G5a). Documenta la scelta nel commit.
- [ ] **Step 2 — Test (TDD).** Scrivi il test che il DTO espone `TurnOrderType` (B: dal toolkit del game; A: dal field). Verifica fallisce.
- [ ] **Step 3 — Implementa** (B: derive nel handler + `SessionDto.TurnOrderType`; A: field+migration+event+handler+hub+DTO). Enum → string (`.ToString()`).
- [ ] **Step 4 — `dotnet build` + test BE pass.**
- [ ] **Step 5 — Commit** `feat(api): #2483 expose turnOrderType in session DTO (path B|A)`.

### Task 2 (FE): `useLiveSessionStore.turnOrderType` + setter

**Files:** Modify `apps/web/src/lib/stores/live-session-store.ts`; (path A) `useSignalrSession.ts`. Test: store test.

- [ ] **Step 1 — Test (TDD)**: lo store espone `turnOrderType: TurnOrderType | null`, popolato dal DTO iniziale (e da SignalR `'TurnOrderTypeUpdated'` se A). Fallisce.
- [ ] **Step 2 — Implementa** il field + setter (mirror `scoringType`/`setScoringConfig` a `live-session-store.ts:59-81`). Path A: aggiungi handler `'TurnOrderTypeUpdated'` in `useSignalrSession.ts` (mirror `'ScoringConfigured'` :133-143).
- [ ] **Step 3 — `pnpm -C apps/web typecheck` + store test pass.**
- [ ] **Step 4 — Commit** `feat(web): #2483 useLiveSessionStore.turnOrderType field`.

### Task 3 (FE): adapter `mapTurnDataToTurnState`

**Files:** Create `apps/web/src/lib/session-live/map-turn-data-to-turn-state.ts`; Test `__tests__/map-turn-data-to-turn-state.test.ts`.

- [ ] **Step 1 — Test (TDD)**: per ognuno dei 7 `TurnOrderType` + un valore unknown, l'adapter produce il `TurnState` corretto (campi per-variant da `turn-state.ts:27-58`, popolati da `currentTurn`/`totalTurns`/`activePlayerId`/`playOrder`). Mapping BE enum (`Free`→`None`, ecc) + `FirstPlayerToken` (FE-only). Fallisce.
- [ ] **Step 2 — Implementa** `mapTurnDataToTurnState(turnOrderType, sessionData) → TurnState` (pure, switch discriminato + tabella mapping enum→union + default→variante sicura per `UnknownBranch`). Mirror `mapScoreDataToPanelData` (`score-data-to-panel-data.ts:35-108`).
- [ ] **Step 3 — typecheck + adapter test pass (tutti 7 + unknown).**
- [ ] **Step 4 — Commit** `feat(web): #2483 mapTurnDataToTurnState adapter`.

### Task 4 (FE): wire `SessionLiveView` (rimuovi hardcoded)

**Files:** Modify `SessionLiveView.tsx:606-615`. Test: view test (rende branch giusto).

- [ ] **Step 1 — Test (TDD)**: dato `turnOrderType='Sequential'` nello store, `SessionLiveView` rende il branch Sequential (non RoundRobin). Fallisce (oggi hardcoded RoundRobin).
- [ ] **Step 2 — Implementa**: sostituisci il `turnRendererState` hardcoded con `mapTurnDataToTurnState(store.turnOrderType ?? <fallback>, activeSession)`. Mantieni il fallback sicuro (se `turnOrderType` null → `UnknownBranch` o un default documentato, NON hardcoded RoundRobin silenzioso).
- [ ] **Step 3 — typecheck + view test pass.**
- [ ] **Step 4 — Commit** `feat(web): #2483 wire SessionLiveView turnRendererState from store`.

### Task 5: finalize + PR

- [ ] **Step 1 — Gate**: `dotnet build` + BE test (SessionTracking); `pnpm -C apps/web typecheck && pnpm -C apps/web lint`; FE test (`src/lib/session-live`, `src/lib/stores`, session-live view).
- [ ] **Step 2 — PR** verso `main-dev`: riepilogo (B|A scelto, store+adapter+view, enum mapping), `Closes #2483`, link epic #2354 + pattern #2389. Footer Claude Code.

---

## Self-Review
- **Spec coverage**: §2 BE source B/A → Task 1 ✓; §4 FE (store/adapter/view) → Task 2/3/4 ✓; §5 enum mapping → Task 3 ✓; §6 acceptance → Task 1-5 ✓.
- **Placeholder scan**: i `<fallback>`/`<path B|A>` sono decisioni risolte in Task 1/4 (criterio esplicito), non placeholder vaghi. Ogni task ha file + template ref + TDD.
- **Type consistency**: `turnOrderType: TurnOrderType | null` (store), `mapTurnDataToTurnState(turnOrderType, sessionData): TurnState`, DTO `TurnOrderType: string?`. Coerenti.
- **Note**: il renderer (`TurnIndicatorRenderer`) + `turn-state.ts` NON cambiano (G5b done). `TurnOrderMethod`/`TurnOrderJson` (Session) restano invariati (player ordering, concetto diverso).
