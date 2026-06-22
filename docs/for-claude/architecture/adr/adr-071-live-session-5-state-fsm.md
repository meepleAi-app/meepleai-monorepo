# ADR-071 — Live Session 5-State FSM Formalization

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 2 — US-INT-4 (session live shell)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · issue #2356 (G7 `SessionStateRenderer`) · issue #2355 (G4 `LiveTopBar` elapsed + connection pip) · issue #2281 (session skeleton G2-G4-G7 scope spec)

## Context

Two distinct state machines currently govern the live session surface. They share terminology but serve different layers:

**Layer 1 — Base UI FSM** (`apps/web/src/lib/session-live/session-live-state.ts`): a 4-state machine derived from TanStack Query state + URL param:
- `loading` — fetch in-flight
- `error` — fetch failed
- `not-found` — null sessionId or 404 from backend
- `default` — data present, healthy

This module comments: "unlike list-view FSMs (Wave D.1), this route has no 'empty' state. The session is a single entity: either present (default) or absent (not-found)."

**Layer 2 — SSE connection state** (`apps/web/src/lib/session-live/use-session-live-stream.ts`): a 5-state machine over the EventSource lifecycle:
- `connecting` — EventSource opening
- `connected` — at least one message received
- `reconnecting` — exponential backoff retry (up to 5 attempts, delays: 1s→2s→4s→8s→16s)
- `degraded-polling` — max retries exhausted, polling fallback active
- `failed` — 429 heuristic (immediate CLOSED before first message) or explicit failure

**Layer 3 — `SessionStateRenderer` discriminated union** (`apps/web/src/components/features/session-live/SessionStateRenderer.tsx`, G7, issue #2356): a 5-state union consumed by panel components:
- `default` — children passthrough
- `empty` — no content for this panel
- `loading` — panel-level skeleton
- `error` — panel-level error banner with retry CTA
- `sse-disconnect` — `ConnectionLostBanner` kind='failed'

These three layers are currently independent. `SessionLiveView.tsx` manually maps between them:
- Base UI FSM state → renders `LoadingShell`, `ErrorShell`, `NotFoundShell`, or the full layout.
- SSE connection state → `showConnectionBanner` boolean condition + `ConnectionLostBanner kind` switch.
- `SessionStateRenderer` is used by child panel components to render their own state — it is not wired to the connection state in the orchestrator yet (the G7 primitive exists but the orchestrator-to-panel propagation is the Tier 3 task).

The spec doc (`2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md`, referenced in `SessionStateRenderer.tsx`) defines the 5 canonical session-live states as: `default | empty | loading | error | sse-disconnect`. This is exactly the `SessionLiveState` type in `SessionStateRenderer.tsx` (renamed `sse-disconnect` vs `sse` in the issue title).

**Known design tension**: the `session-live-state.ts` file explicitly says there is no `empty` state for the session route — the session entity is either present or absent. Yet `SessionStateRenderer` exposes an `empty` kind. The `empty` kind is for **panel-level** emptiness (e.g., action log has no entries), not route-level emptiness. The two uses of "empty" are distinct.

**State storage**: currently, the base UI FSM is pure derived state (`deriveSessionLiveUiState` is a pure function called in `useMemo`), URL search params drive dialog and tab state, and SSE state lives in `useSessionLiveStream`'s `useReducer`. No Zustand store is involved for session-live state.

**`LiveTopBar` connection pip** (G4, issue #2355): `mapConnectionState(liveStream.connectionState)` maps `SseConnectionState → 'connected' | 'reconnecting' | 'failed'` for a visual pip in the top bar. This mapping is already in `apps/web/src/lib/session-live/map-connection-state.ts`.

## Problem

The specific architectural question: **how should the 5-state `SessionStateRenderer` union be formally connected to the session orchestrator's state derivation, such that panel components always receive the correct state kind; and what are the formal transition rules between the 5 states, including SSE error recovery?**

Sub-decisions:
1. **State storage**: where does the "effective panel state" live — derived in each panel via `useMemo`, or computed centrally in the orchestrator and passed as a prop?
2. **Transition guards**: plain conditional logic vs explicit reducer vs XState (or similar).
3. **SSE error recovery**: auto-retry on `error` → `loading` vs manual user action (retry button).
4. **`empty` vs `default` distinction**: panel-level vs route-level — should `empty` be a prop to the panel or a distinct state in the orchestrator?

## Options Considered

### Option A — State machine library (XState or Zustand with machine plugin)

Introduce a formal FSM library. States, transitions, guards, and side effects are declared in a machine definition. The orchestrator (`SessionLiveView.tsx`) subscribes to the machine state.

**Pros**:
- Explicit transition table — guards and side effects are auditable.
- XState DevTools visualise state at runtime (useful for debugging SSE edge cases).
- Formal state guarantees: impossible states are unreachable by construction.

**Cons**:
- No FSM library is present in the codebase (`package.json` lists no `xstate`, `@xstate/*`, or FSM-adjacent packages). Adding one introduces a new dependency category.
- XState v5 has a significant API surface to learn and document. The team has not adopted it elsewhere.
- The existing FSM (`deriveSessionLiveUiState`) is a simple 5-cell priority cascade — the complexity of a library is disproportionate.
- Zustand-with-machine is not a documented pattern in the codebase.

**Risks**: Dependency risk. Knowledge concentration. Over-engineering for a 5-state machine.

**Impact**: ~4 days + dependency onboarding.

---

### Option B — Centralized Zustand store for session-live state

A `useSessionLiveStore` Zustand store holds the "effective panel state" and is updated by the orchestrator. Panel components subscribe to the store.

**Pros**:
- Zustand is already used in the project (Asse B shipped `DrawerStack` with Zustand cascade store pattern).
- Global state access: any panel component can read the connection state without prop drilling.
- Zustand's `subscribeWithSelector` allows per-field subscriptions, avoiding over-render.

**Cons**:
- The live session view is a single page with a well-defined component tree — global store state is not needed; prop-passing is sufficient.
- `useSessionLiveStream` already manages SSE state in `useReducer` locally. Duplicating it into a Zustand store creates a state sync obligation.
- The existing `compose-session-live-state.ts` pattern is a pure reducer with no Zustand dependency — importing a Zustand store would mix functional and reactive patterns.

**Risks**: State synchronisation bugs if Zustand store and `useSessionLiveStream`'s `useReducer` diverge.

**Impact**: ~2 days. New store file + refactor of `SessionLiveView.tsx`.

---

### Option C — Plain reducer + prop propagation (recommended)

The orchestrator (`SessionLiveView.tsx`) derives the effective `SessionLiveState` for each panel as a `useMemo` computation over existing state. The derived `SessionLiveState` is passed as a prop to each panel component (or to `SessionStateRenderer` wrapping each panel).

The 5-state `SessionLiveState` (per `SessionStateRenderer` types) is derived as follows:

```
Precedence (highest → lowest):
  sse-disconnect    when: connectionState ∈ {failed, degraded-polling}
  loading           when: uiState === 'loading'
  error             when: uiState === 'error'
  empty             when: panel-specific condition (e.g., actionLog.length === 0)
  default           otherwise
```

Transition rules:
- `sse-disconnect`: SSE connection is `failed` (429) or `degraded-polling` (5 retries exhausted). Overlay on the panel — does not replace the session shell.
- `loading → default`: TanStack Query resolves with data.
- `loading → error`: TanStack Query resolves with error; `onRetry` calls `sessionQuery.refetch()`.
- `error → loading`: manual retry triggered (user clicks "Retry").
- `default → sse-disconnect`: SSE fires 5 retries and enters `degraded-polling`; or 429 heuristic triggers `failed`. Session data remains in `default`; banner overlays the panel.
- `sse-disconnect → default` (recovery): user clicks manual retry (`liveStream.reconnect()`); connection returns to `connected`; `showConnectionBanner` becomes false; panel returns to `default`.

The `empty` kind is **panel-local**, not derived from the FSM:
- `ActionLogTimeline` receives `kind: 'empty'` when `actionLog.length === 0`.
- `PlayerRosterLive` never receives `kind: 'empty'` (roster always has at least one player in a valid session).
- `LiveScoringPanel` receives `kind: 'empty'` only if `scores.length === 0` (edge case: session with no players).

**Pros**:
- No new libraries or store infrastructure.
- `SessionLiveState` derivation is a pure function in `useMemo` — fully unit-testable.
- The existing `deriveSessionLiveUiState` function covers base UI states; the new derivation adds SSE layer on top.
- Panel components receive a prop-typed `SessionLiveState` — TypeScript enforces exhaustiveness (per `assertNever` in `SessionStateRenderer`).
- `empty` vs `default` distinction is cleanly resolved: `empty` is a panel-level prop decision by the orchestrator, not a global FSM state.

**Cons**:
- SSE state and base UI state are composed in `SessionLiveView.tsx` via `useMemo` — the derivation logic lives in the orchestrator, not in a dedicated module. As panel count grows, this `useMemo` becomes more complex.
- No visualisation tool for FSM debugging (no XState DevTools).

**Risks**: Low. The derivation pattern mirrors `deriveSessionLiveUiState` exactly — same pure-function, same unit-test approach.

**Impact**: ~1.5 days. New `deriveSessionPanelState(uiState, connectionState, panelData)` pure function in `session-live-state.ts` + integration in `SessionLiveView.tsx`.

---

### Option D — URL search params as FSM state SSOT

Extend the existing URL search param SSOT (`?state=loading|not-found` already exists for override) to include `?connectionState=failed|degraded-polling` in production.

**Pros**: Deep-linkable FSM states for debugging.

**Cons**: SSE connection state is transient runtime state — serialising it to URL creates spurious history entries and breaks the browser back button. The existing `?state=` override is a dev/visual-test hatch guarded by `STATE_OVERRIDE_ENABLED` — not intended for production state management.

**Risks**: Confusing UX (URL changes during live session). Not appropriate.

**Impact**: Out of scope.

## Decision

**Adopt Option C**: plain reducer + prop propagation using a new `deriveSessionPanelState` pure function.

The 5-state union (`SessionLiveState` in `SessionStateRenderer`) is the canonical component-facing API. The orchestrator derives the correct state per panel from the existing base UI FSM + SSE connection state. No new library or global store is introduced.

**SSE error recovery rule**: auto-retry is handled by `useSessionLiveStream` (exponential backoff, up to 5 retries). After 5 retries, state transitions to `degraded-polling` (banner shown) + polling fallback (`useSession` with `refetchInterval: 5000`). Manual retry (`liveStream.reconnect()`) is exposed only when `connectionState ∈ {degraded-polling, failed}`. Recovery to `default` is automatic when `connectionState` returns to `connected` (the `showConnectionBanner` condition clears).

**`empty` vs `default` resolution**: `empty` is exclusively a panel-local decision by the orchestrator — never a global FSM state. The `session-live-state.ts` comment ("no empty state for the session route") remains correct at the route level. At the panel level, `empty` is a prop-passing pattern, not an FSM transition.

## Consequences

**Positive**:
- TypeScript enforces exhaustiveness at the `SessionStateRenderer` switch — adding a 6th state kind requires code changes and TypeScript will catch missed cases (`assertNever`).
- `deriveSessionPanelState` is a pure function — unit-testable without React.
- The SSE recovery flow (retry button → `liveStream.reconnect()` → `RESET` → `CONNECTING` → `CONNECTED`) is already fully implemented in `useSessionLiveStream` — this ADR formalises the visual mapping, not new behavior.
- `degraded-polling` path (`refetchInterval: 5000`) provides graceful degradation for users in poor network conditions without session loss.

**Negative**:
- As the panel count in `SessionLiveView.tsx` grows beyond the current 5 panels, the orchestrator's `useMemo` derivations multiply. Extract to a dedicated `useSessionPanelStates()` hook if this becomes unwieldy.
- No runtime FSM visualisation. Debugging requires console logging or test fixtures (the existing `?state=` URL override + `IS_VISUAL_TEST_BUILD` flag covers most cases).

**Trade-offs**:
- `sse-disconnect` overlays the panel but does not replace the session data already rendered. This is intentional: a `degraded-polling` user can still see the last known scores and roster — they just know the live feed is degraded. The alternative (blank panel on disconnect) would be more alarming and less useful.
- The `not-found` base UI state has no `SessionStateRenderer` equivalent (it is a route-level shell, not a panel-level state). This is correct — a missing session renders a full-page `NotFoundShell`, not a panel state.

## Implementation Guidance

1. **New pure function**: `deriveSessionPanelState(uiState: SessionLiveUiState, connectionState: SseConnectionState, isEmpty: boolean): SessionLiveState` in `apps/web/src/lib/session-live/session-live-state.ts`.

   ```typescript
   export function deriveSessionPanelState(
     uiState: SessionLiveUiState,
     connectionState: SseConnectionState,
     isEmpty: boolean,
     // Labels injected by orchestrator
     labels: { errorTitle: string; retryLabel: string; emptyTitle: string },
     onRetry: () => void,
     error: Error | null,
   ): SessionLiveState {
     if (connectionState === 'failed' || connectionState === 'degraded-polling') {
       return { kind: 'sse-disconnect', connectionLabels: ... };
     }
     if (uiState === 'loading') return { kind: 'loading', loadingAriaLabel: ... };
     if (uiState === 'error') return { kind: 'error', error: error ?? new Error('Unknown'), onRetry, errorTitle: labels.errorTitle, retryLabel: labels.retryLabel };
     if (isEmpty) return { kind: 'empty', title: labels.emptyTitle };
     return { kind: 'default', children: null }; // children supplied by caller
   }
   ```

2. **Integration**: in `SessionLiveView.tsx`, wrap each panel in `<SessionStateRenderer state={...}>` with the derived state. For panels that are never empty (e.g., `PlayerRosterLive`), pass `isEmpty: false` always.

3. **Polling fallback**: when `connectionState === 'degraded-polling'`, mount a `useSession(sessionId, { enabled: true, refetchInterval: 5_000 })` hook (per `useSessionLiveStream` documentation). This provides ~5s-stale data while SSE is down.

4. **Unit tests**: add test cases to `session-live-state.test.ts` for each combination of `uiState × connectionState` that produces a different `SessionLiveState kind`.

5. **Visual test fixtures**: add `?state=sse-disconnect` URL override (guarded by `STATE_OVERRIDE_ENABLED`) for visual baseline snapshots of the `sse-disconnect` panel state.

6. **Existing spec alignment**: the `SessionStateRenderer.tsx` comments reference `2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md §G7` as the canonical spec. This ADR formalises the orchestrator-side wiring described in that spec's §G7 "orchestrator propagation" section.

## Rollback / Reversibility

The `SessionStateRenderer` primitive (G7) and `useSessionLiveStream` (with its `connectionState` output) are already merged and production-ready. This ADR's `deriveSessionPanelState` function is a new pure function — removing it reverts to the current behaviour where panels do not receive `SessionLiveState` props. Rollback = remove the function and the `<SessionStateRenderer>` wrappers around panels. The `ConnectionLostBanner` still shows (via `showConnectionBanner` in `SessionLiveView.tsx`) even without `deriveSessionPanelState`.

## References

- `SessionStateRenderer.tsx` (G7) — `apps/web/src/components/features/session-live/SessionStateRenderer.tsx`
- `session-live-state.ts` (base UI FSM) — `apps/web/src/lib/session-live/session-live-state.ts`
- `use-session-live-stream.ts` (SSE state machine) — `apps/web/src/lib/session-live/use-session-live-stream.ts`
- `compose-session-live-state.ts` (pure reducer) — `apps/web/src/lib/session-live/compose-session-live-state.ts`
- `SessionLiveView.tsx` (orchestrator) — `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- `LiveTopBar.tsx` (G4, connection pip) — `apps/web/src/components/features/session-live/LiveTopBar.tsx`
- `map-connection-state.ts` — `apps/web/src/lib/session-live/map-connection-state.ts`
- Issue #2356 (G7 SessionStateRenderer primitive)
- Issue #2355 (G4 LiveTopBar elapsed + connection pip)
- Issue #2281 (session skeleton G2-G4-G7 scope spec)
- ADR-070 — flavor module loading (sister ADR for panel content)
