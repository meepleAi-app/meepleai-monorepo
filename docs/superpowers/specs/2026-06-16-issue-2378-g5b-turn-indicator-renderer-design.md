# Issue #2378 G5b — TurnIndicatorRenderer polymorphic dispatch (design)

**Date**: 2026-06-16
**Parent epic**: [#2354 Session live shell](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354)
**Sub-issue**: [#2378](https://github.com/meepleAi-app/meepleai-monorepo/issues/2378)
**Effort estimate**: ~3-5gg

## 1. Context

G5b of epic #2354 adds a polymorphic `TurnIndicatorRenderer` that switches on
`turnOrderType` over **7 variants**:

1. `RoundRobin` — turn order rotates among players (e.g. Wingspan, Catan).
2. `Sequential` — fixed phase sequence per round, team-resolved (e.g. Paleo
   Morning/Day/Night).
3. `Simultaneous` — all players act at the same time (e.g. co-op simultaneous).
4. `Realtime` — no turns, parallel real-time play.
5. `None` — free-form, no defined turn order.
6. `Custom` — toolkit-defined phase sequence (also covers the `Free` alias).
7. `FirstPlayerToken` — turn order rotates with a "first player" token (DEC-3,
   not yet in the mockup; the renderer adds it per #2378 body's "7 variants"
   list).

Existing `TurnIndicator.tsx` (LiveAgentChat sibling) implements a RoundRobin-
like progress bar + active player display. Per **DEC-2** it stays as-is and
becomes the **body of the RoundRobin branch**; the other 6 branches are net-new
components composed by the renderer.

The `bg-[hsl(240,60%,65%)]` raw HSL eslint-disable in `TurnIndicator.tsx:84-85`
is pre-existing tech debt (tracked under #807-followup) and is **not** in
scope for this PR.

Mockup canonical: `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx`
(`TurnIndicatorRenderer` lines 289-477).

## 2. Decisions locked

| ID | Decision | Rationale |
|---|---|---|
| DEC-1 | **7 variant** (mockup 6 + FirstPlayerToken) | Issue body says 7; mockup is missing FirstPlayerToken; DEC-3 picks the addition. |
| DEC-2 | Mantenere `TurnIndicator` + new `TurnIndicatorRenderer` parent dispatcher | Reuse existing RoundRobin body. `data-slot="turn-indicator"` moves to the renderer root for E2E selector stability; the legacy RoundRobin body keeps its progress-bar internals. |
| DEC-3 | 7° variant = **FirstPlayerToken** | Most common board-game pattern (Wingspan, Catan, Pandemic). Out of scope: `BiddingForOrder`, `PhaseBased` (future epic). |
| DEC-4 | TypeScript **discriminated union** for `TurnState` | Type-safe per variant + automatic narrowing in `switch`. Verbose but compiler-enforced. |
| DEC-5 | Unknown `turnOrderType` → **warning banner + minimal display** | Match mockup default case (`<M.StateBlock icon="❔" title="turnOrderType X sconosciuto">`). Adds `console.warn` so operators see new BE enum values surface in logs. |

## 3. Component architecture

```
TurnIndicatorRenderer.tsx                  ← new, parent dispatcher
  ├─ RoundRobinBranch                      ← reuses existing TurnIndicator.tsx
  ├─ SequentialBranch                      ← new (PhaseStepper)
  ├─ SimultaneousBranch                    ← new (player grid + day phase strip)
  ├─ RealtimeBranch                        ← new (warning banner + parallel marker)
  ├─ NoneBranch                            ← new (free-form banner)
  ├─ CustomBranch                          ← new (PhaseStepper, toolkit-driven)
  ├─ FirstPlayerTokenBranch                ← new (avatar + rotating token icon)
  └─ UnknownBranch                         ← new (warning banner + player list)

shared:
  PhaseStepper                             ← internal helper (Sequential, Custom)
  TurnTypePill                             ← internal helper (mockup magnet)
```

`data-slot="turn-indicator"` lives on the `<TurnIndicatorRenderer>` root
(replacing its previous home on `TurnIndicator.tsx`). The legacy `TurnIndicator`
component **loses its `data-slot`** to avoid double E2E selectors.

## 4. Type contract

```ts
// apps/web/src/lib/session-live/turn-state.ts (new)

export type TurnOrderType =
  | 'RoundRobin'
  | 'Sequential'
  | 'Simultaneous'
  | 'Realtime'
  | 'None'
  | 'Custom'
  | 'FirstPlayerToken';

export interface PlayerInfo {
  readonly id: string;
  readonly name: string;
  readonly avatarUrl?: string;
}

export type TurnState =
  | { readonly type: 'RoundRobin'; readonly round: number; readonly totalRounds: number;
      readonly activePlayerId: string; readonly playOrder: ReadonlyArray<string>; }
  | { readonly type: 'Sequential'; readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number; }
  | { readonly type: 'Simultaneous'; readonly phases?: ReadonlyArray<string>;
      readonly activePhaseIndex?: number; }
  | { readonly type: 'Realtime'; }
  | { readonly type: 'None'; }
  | { readonly type: 'Custom'; readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number; }
  | { readonly type: 'FirstPlayerToken'; readonly round: number; readonly totalRounds: number;
      readonly tokenHolderId: string; readonly playOrder: ReadonlyArray<string>; };
```

The renderer prop:

```ts
export interface TurnIndicatorRendererProps {
  readonly state: TurnState;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}
```

## 5. Labels interface

```ts
export interface TurnIndicatorRendererLabels {
  // ICU-resolved-by-parent (Gate A); 7 variant headings + your-turn/waiting +
  // the unknown fallback message.
  readonly roundRobinHeading: string;
  readonly sequentialHeading: string;
  readonly simultaneousHeading: string;
  readonly realtimeHeading: string;
  readonly noneHeading: string;
  readonly customHeading: string;
  readonly firstPlayerTokenHeading: string;
  readonly unknownTitle: string;            // "Tipo di turno non supportato"
  readonly unknownBody: string;             // "Aggiorna l'app per supportare questa modalità."
  readonly yourTurnLabel: string;
  readonly waitingLabel: string;
  // RoundRobin extras (forwarded to TurnIndicator's existing labels)
  readonly roundCountTemplate: string;      // "Round {round} di {total}"
  readonly playOrderHeading: string;
  // FirstPlayerToken extras
  readonly firstPlayerTokenHolderTemplate: string;  // "Il token primo giocatore è di {playerName}"
}
```

## 6. A11y

- `<TurnIndicatorRenderer>` root has `role="region"` + `aria-label={resolved heading}`.
- Active-turn-affecting branches (`RoundRobin`, `FirstPlayerToken`,
  `Sequential`, `Custom`) wrap their active indicator in
  `aria-live="polite"` so screen readers announce turn changes.
- `Simultaneous` / `Realtime` / `None` use `role="status"` for the banner.
- Each branch is independently keyboard-navigable; no focus traps; no
  interactive elements except the optional "your turn" CTA in `RoundRobin`
  (covered by existing `TurnIndicator`).

## 7. Failure handling

- Unknown `state.type` → `UnknownBranch` renders a warning banner with the
  unsupported type, plus a `console.warn('[TurnIndicatorRenderer] Unknown
  turnOrderType:', state.type)`. The renderer does NOT throw.
- Missing `players` array → each branch falls back to a single "in attesa di
  dati" placeholder.
- `activePlayerId` not in `players` → branch renders the active block as
  "Sconosciuto" (no crash).
- Empty `phases` for Sequential/Custom → renders a `<p>` skeleton with the
  empty-state label.

## 8. Testing strategy

- **Unit (Vitest)**: 1 test per branch (7) + 1 test for the unknown fallback
  + 1 test for missing `activePlayerId` → ~9 tests minimum.
- **Integration**: extend `SessionLiveView.test.tsx` to swap the existing
  `<TurnIndicator>` consumer for `<TurnIndicatorRenderer>` with a sample
  `state.type='RoundRobin'`. Verify the right column 'turn' tab still renders
  + the same `data-slot="turn-indicator"` selector resolves.
- **Mobile**: extend the existing mobile bottom-sheet test to cover at least
  one variant other than RoundRobin to prove the polymorphic dispatch works
  in compact mode.
- **axe AA**: 1 axe test covering all 7 + unknown states (8 total
  re-renders, each `expect(results).toHaveNoViolations()`).
- **i18n**: 14 new keys (7 headings + unknown title/body + roundCount template +
  playOrderHeading + firstPlayerTokenHolderTemplate + reused yourTurn/waiting).
  Add to `apps/web/src/locales/{it,en}.json` under `pages.sessionLive.turnIndicator`.

## 9. Acceptance criteria (issue body)

- [x] Dispatch corretto per ogni TurnOrderType (7 variant) — DEC-1+3 lock.
- [x] Indicator visivo: turno attivo + prossimo turno + ordine completo —
  RoundRobin + FirstPlayerToken (others by-design have no individual turn).
- [ ] axe AA 0 violations + `aria-live` per turn change — §6.
- [ ] Unit test 1-per-variant — §8.

## 10. Out of scope

- `bg-[hsl(240,60%,65%)]` token migration in `TurnIndicator.tsx` (tracked under
  #807-followup; not blocked by G5b).
- `BiddingForOrder`, `PhaseBased`, mockup commission — future epic.
- Backend `TurnOrderMethod` enum changes — current `Manual`/`Random` is a
  different semantic (who decides ordering, not which pattern); FE
  `TurnOrderType` is toolkit-driven.
- `SessionLiveView` integration that switches RIGHT-column 'turn' tab from
  `<TurnIndicator>` to `<TurnIndicatorRenderer>` with real `state` derived
  from `useLiveSessionStore` — handled by **#2389 follow-up** (the same
  follow-up that gates G5a). For G5b we just expose the renderer + replace
  the existing call site with a `<TurnIndicatorRenderer state={…}>` that
  defaults to `type='RoundRobin'` (fixture-driven).

## 11. Refs

- Epic: [#2354](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354).
- Mockup: `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx`
  lines 289-477.
- Backend enum: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/TurnOrderMethod.cs`
  (orthogonal — DOES NOT map 1:1 to FE TurnOrderType).
- Existing component: `apps/web/src/components/features/session-live/TurnIndicator.tsx`.

🤖 Brainstormed via main-thread Q/A — 5 DEC user-locked.
