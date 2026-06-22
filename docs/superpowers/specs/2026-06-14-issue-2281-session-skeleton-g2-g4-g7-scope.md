# Issue #2281 — Session Skeleton G2+G4+G7 Scope

> **Status**: Decision-only spec (no code shipped in the same PR). Lands the
> architectural decisions for G2 (URL pattern) + scopes the implementation
> tasks for G4 (TopBar) + G7 (canonical states). Implementation PRs follow.
>
> **Issue**: [#2281](https://github.com/meepleAi-app/meepleai-monorepo/issues/2281) — `/sessions/[id]` vs `sp4-session-skeleton-live` conformance
> **Priority**: P2 (mockup conformance, no functional bug)
> **Mockup**: `admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`

---

## TL;DR

| Gap | Decision | Effort |
|---|---|---|
| **G2** — URL pattern (child routes vs `?tab=`) | **Keep current child-route structure.** The mockup's `?tab=` query-param is _not_ adopted. | 0gg (decision only) |
| **G4** — TopBar with live timer + connection status | **Extend existing `LiveTopBar.tsx` with 2 new optional props.** No new component. | 1-2gg |
| **G7** — 5 canonical states (`default`/`empty`/`loading`/`error`/`sse-disconnect`) | **Introduce `SessionStateRenderer` primitive.** Polymorphic discriminated-union wrapper. | 1-2gg |
| G1 / G3 / G5 / G6 | Out of scope — deferred to a separate epic (multi-week scope per the original issue). | — |

Total scoped effort: **~2-4gg** for one developer.

---

## G2 — URL pattern decision

### Mockup proposal
`sp4-session-skeleton-live` uses `?tab=tools` query-param for the right-pane
polymorphic tabs (Score / Turni / Widget / Note).

### Current implementation (since Wave D.2/D.3)

```
/sessions/[id]/             # post-game summary
/sessions/[id]/live         # live shell
/sessions/[id]/live/play    # current-turn play surface
/sessions/[id]/live/players # player roster
/sessions/[id]/live/scoreboard
/sessions/[id]/live/notes
/sessions/[id]/play/<...>   # play-by-play history (read-only)
```

### Decision — **keep child routes**

Drivers:

1. **Browser-history correctness.** Child routes give every tab a discrete
   history entry. Back / forward / deep-link all work without query-param
   parsing. The `?tab=` pattern requires synthetic history pushes that
   degrade the back button.
2. **Code splitting.** Next.js App Router lazy-loads per route segment. The
   `live/play/page.tsx` bundle is fetched only when the user opens that
   tab. With `?tab=`, all panes would ship in the single live bundle.
3. **Auth + redirect simplicity.** Each child route has its own
   `layout.tsx` + `error.tsx` boundary. The brownfield FORK in
   `SessionSummaryView` already redirects in-progress sessions to
   `/sessions/[id]/live` — adding a synthetic `?tab=` adapter on top
   adds two redirect hops with no upside.
4. **Mockup is design-intent, not contract.** The session-skeleton mockup
   was authored as a single HTML file (`?tab=` is HTML-natural). The
   underlying user behavior is identical for either URL shape.

### Action items

- **Update mockup `sp4-session-skeleton-live`.** Replace the `?tab=` markup
  with anchor `href` placeholders pointing to `/sessions/[id]/live/<tab>`.
  Filed as a follow-up — the cleanup belongs to a designer pass, not this
  spec.
- **No code changes required** for G2. The decision _ratifies_ the
  current implementation; the gap is closed by the mockup update.

---

## G4 — TopBar with live timer + connection status

### Mockup spec

The universal TopBar shows:

- Back link (`← Sessioni`)
- Session title
- **Live elapsed timer** (`00:42:13` — counting up since `startedAt`)
- **Connection status pip** (green = SSE connected, amber = reconnecting, red = failed)
- Endgame CTA (host-only)

### Current implementation

`apps/web/src/components/features/session-live/LiveTopBar.tsx` (177 lines)
already renders:

- Session name + status (`InProgress`/`Paused`)
- Turn label (pre-resolved ICU plural)
- Role-based CTAs (`Pause`/`Resume`/`Endgame`/`Exit`)

What's missing:

- **Live elapsed timer** — currently absent.
- **Inline connection status** — handled by a SEPARATE `ConnectionLostBanner`
  banner _below_ the TopBar; the mockup wants it _in_ the TopBar as a pip.

### Decision — **extend `LiveTopBar`, do not create a new component**

Add 2 optional props:

```ts
export interface LiveTopBarProps {
  // …existing fields…

  /** Live elapsed time in ms since session start. Omit to hide the timer. */
  readonly elapsedMs?: number;

  /** SSE connection state. Omit to hide the inline pip (default behavior). */
  readonly connectionState?: 'connected' | 'reconnecting' | 'failed';
}
```

Behavior:

- When `elapsedMs` is provided, render a monospace `HH:MM:SS` chip next to
  the status badge. Format via existing `formatElapsedTime()` helper
  (`apps/web/src/lib/session-live/format-elapsed-time.ts` — already
  shipped, currently used by `LiveScoringPanel`).
- When `connectionState` is provided, render a colored pip
  (3px circle, entity colors): emerald for `connected`, amber for
  `reconnecting`, destructive for `failed`. Tooltip on hover (handled by
  the existing `Tooltip` primitive in `components/ui/tooltip`).
- Both props remain optional so existing call sites (no timer, no inline
  status) keep working without modification.
- The standalone `ConnectionLostBanner` stays in place for the
  `degraded-polling` and `failed` (retry CTA) states — the pip is a
  glanceable summary, the banner is the actionable surface.

### Action items

- Implementation PR: extend `LiveTopBar.tsx` with the 2 new props
  (≤30 LOC delta).
- Wire `elapsedMs` from the existing `useSessionLiveStore` selector
  `selectElapsedMs()`.
- Wire `connectionState` from `useSessionLiveStore.connectionStatus`
  via a small mapping function (the store's `connectionStatus` field
  has more granular states; map to the 3-value TopBar enum).
- Unit tests: 3 cases (timer rendered / not rendered / connectionState
  variants).

---

## G7 — 5 canonical states standardized

### Mockup spec

`sp4-session-skeleton-live` documents 5 canonical states the live surface
must render:

| State | Trigger | Visual |
|---|---|---|
| `default` | Session live, SSE connected, data present | Full UI |
| `empty` | Session has no events / no players yet | Empty-state with CTA |
| `loading` | Initial fetch in flight | Skeleton primitives |
| `error` | Backend returned an error | Inline error banner + retry |
| `sse-disconnect` | SSE stream lost | `ConnectionLostBanner` |

### Current implementation

States are handled ad-hoc:

- `loading` — `Suspense` fallback in `page.tsx` + per-component shimmer.
- `error` — `error.tsx` boundary + per-component fallback.
- `sse-disconnect` — `ConnectionLostBanner` rendered conditionally inside
  `LiveSessionView`.
- `empty` — no consistent pattern (each tab has its own).
- `default` — implicit (no special handling).

### Decision — **`SessionStateRenderer` primitive**

Introduce a typed discriminated-union renderer:

```tsx
// apps/web/src/components/features/session-live/SessionStateRenderer.tsx
type SessionLiveState =
  | { kind: 'default'; children: ReactNode }
  | { kind: 'empty'; emptyCta: ReactNode; message: string }
  | { kind: 'loading' }
  | { kind: 'error'; error: Error; onRetry: () => void }
  | { kind: 'sse-disconnect' };

export function SessionStateRenderer(state: SessionLiveState): ReactElement {
  switch (state.kind) {
    case 'default':         return <>{state.children}</>;
    case 'empty':           return <EmptyState message={state.message} cta={state.emptyCta} />;
    case 'loading':         return <SessionLoadingSkeleton />;
    case 'error':           return <SessionErrorBanner error={state.error} onRetry={state.onRetry} />;
    case 'sse-disconnect':  return <ConnectionLostBanner kind="failed" labels={…} />;
  }
}
```

Why a primitive:

- **Exhaustiveness.** TypeScript verifies the switch covers all 5 states
  — adding a 6th requires a code change AND a type change, no silent
  drift.
- **Consistency.** Every live-surface tab (`play`, `players`,
  `scoreboard`, `notes`) routes through the same renderer, so the empty
  state in scoreboard looks identical to the empty state in notes.
- **Storybook surface.** Each tab gets 5 Stories instead of 5 ×
  N-bespoke-implementations. Designer review becomes O(state) instead of
  O(state × tab).

### Action items

- Implementation PR: scaffold `SessionStateRenderer.tsx` + a small set of
  `Empty*` primitives (icon 96px + tagline + CTA, per
  `admin-mockups/README.md` § State variants guidance).
- Wire one tab first (`/sessions/[id]/live/scoreboard` — simplest empty
  state) as the pilot. Other tabs follow.
- Add 5 Storybook variants per `SessionStateRenderer` consumer.
- DS-17 #2071 state-naming convention applies: any Storybook fixture
  files use `<base>-state-NN-<label>` per the canonical catalog.

---

## Out of scope (deferred)

The remaining gaps from the issue require multi-week effort and are not
scoped here:

| Gap | Effort | Defer to |
|---|---|---|
| **G1** — 3-column desktop layout (LEFT 60% Chat+log · RIGHT 40% polymorphic tabs) | 4-6gg | Future epic (requires `Chat` + log primitives that don't exist yet) |
| **G3** — ChatAgent always-visible | 2-3gg (depends on G1) | Same epic as G1 |
| **G5** — Polymorphic renderers (`ScoringPanelRenderer` / `TurnIndicatorRenderer` / `ToolkitRenderer`) | 3-5gg per renderer | Future epic |
| **G6** — Game-specific extensions (Catan / Wingspan / Paleo / Codenames / Power Grid / Puerto Rico / Zombicide) | 4-8 weeks per game | Per-game epic |

---

## Acceptance criteria for this PR

This PR ships only the **decision-only spec doc**. Code implementation
follows in separate PRs:

- [x] G2 decision documented (keep child routes; update mockup).
- [x] G4 scope: 2-prop extension to `LiveTopBar` (≤30 LOC delta).
- [x] G7 scope: `SessionStateRenderer` primitive + pilot tab.
- [ ] Implementation PRs filed (separate, owners TBD).

## Refs

- Issue: [#2281](https://github.com/meepleAi-app/meepleai-monorepo/issues/2281)
- Mockup: `admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`
- Game-specific extension (Catan): `admin-mockups/design_files/sp4-session-catan-live.{html,jsx}`
- Existing components:
  - `apps/web/src/components/features/session-live/LiveTopBar.tsx`
  - `apps/web/src/components/features/session-live/ConnectionLostBanner.tsx`
- Related: #2088 (functional fix for the 404 empty state — landed earlier)
- DS-17 §2071: state naming convention used by G7 fixtures
