# Plan: Issue #2373 — ScoringPanelRenderer Polymorphic Dispatch (G5a)

> Generated 2026-06-15 via `Plan` subagent dispatched during `/sc:spec-panel "find next 5 issues"` session. Sub-issue G5a of epic #2354 (Session live shell). Parallel to #2374 (G1 layout) — renderer is layout-agnostic.

## §1 Context + spec links

- **Issue**: [#2373](https://github.com/meepleAi-app/meepleai-monorepo/issues/2373) — `feat(session-live): ScoringPanelRenderer polymorphic dispatch (4 ScoreType variants)`
- **Epic**: [#2354](https://github.com/meepleAi-app/meepleai-monorepo/issues/2354) — Session live shell (sub-issue G5a)
- **Sibling G1**: #2374 (layout). G5a is layout-agnostic.
- **Canonical mockup**: `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` (Panel-based dispatch on `data.scoring.scoreType` — Points / Ranking / BinaryWin / Objectives variants, lines 100–286).
- **Backend asse A v2.1**: SHIPPED (PR #1917) — `ScoreType.cs` enum, `IScoringStrategy`, `ScoringStrategyFactory`, `UpdateSessionScoresCommand`, `MAX_LIVE_SESSIONS_EXCEEDED`. Per epic body, asse-A polymorphic Score wiring landed via PR #1896 sess.32 (`c1efb4fb6`).
- **Asse D P1 (FE editor) — SHIPPED sess.35**: `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx`, the 4 strategy editors, `useUpdateSessionScores` mutation hook with `UpdateSessionScoresError` tagged union (`forbidden|validation|server`). Plan at `docs/superpowers/plans/2026-06-05-asse-d-p1-polymorphic-score-editor.md`.
- **Token discipline**: `CLAUDE.md` § Token Canonicalization — only semantic tokens (`bg-card`, `bg-muted`, `text-foreground`, `border-border`, `text-muted-foreground`) + entity utilities (`bg-entity-session`, `text-entity-toolkit`); ESLint `local/no-hardcoded-color-utility` is **error** since DS-15.
- **Asse A semantic alignment**: `docs/superpowers/plans/2026-06-04-asse-a-semantic-alignment.md`.
- **Issue #2281 / sister scope**: `docs/superpowers/specs/2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md`.

## §2 Discovery notes (what's already shipped vs the gap)

### BE shipped (mirror checkpoint — no FE-side blocker)

- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs` — 4 variants.
- `ScoringStrategyFactory.cs` + 4 strategies (`Points|Ranking|BinaryWin|Objectives`).
- `UpdateSessionScoresCommand` ready end-to-end (per epic body, commit `c1efb4fb6`).

### FE primitives shipped (sess.35, asse D P1)

| Concern | File | Notes |
|---|---|---|
| Editor dispatcher (WRITE) | `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx` | Exhaustive `switch` over `ScoreType`; throws on missing `availableObjectives`; emits `ScoreChangePayload` tagged union. |
| Editor strategies (4) | `apps/web/src/components/sessions/score-strategies/{Points,BinaryWin,Objectives,Ranking}Editor.tsx` | DnD ranking via `@dnd-kit`. |
| Type contract | `apps/web/src/components/sessions/score-strategies/types.ts` (lines 19, 24–58) | `ScoreType = 'Points' \| 'BinaryWin' \| 'Objectives' \| 'Ranking'`, `ScoreDataByType` map, `PlayerOption { id, displayName, avatar? }`. |
| Mutation hook | `apps/web/src/hooks/use-update-session-scores.ts` | Returns `UpdateSessionScoresError { kind: 'forbidden'\|'validation'\|'server' }`. Invalidates `['session', id]` + `['live-session', id]`. |
| Schemas | `apps/web/src/lib/api/schemas/toolkit.schemas.ts` (line 137) | `ScoreTypeSchema = z.enum(['Points','Ranking','BinaryWin','Objectives'])` — order differs from `score-strategies/types.ts` but value set is identical. |

### Pre-existing read-side renderer (toolkit context — DO NOT reuse directly)

- `apps/web/src/components/toolkit/ScoringPanelRenderer.tsx` (issue #1749 B19-4a) already dispatches on `AiScoringTemplateSuggestion.scoreType` (Points/Ranking/BinaryWin/Objectives) for the **toolkit template preview**. Its data shape is `template + scores: Record<string, number>`. It is a **read-only chip/list display** with no host vs viewer differentiation, no edit handles, and no live-session store coupling.
- Tests at `apps/web/src/components/toolkit/__tests__/ScoringPanelRenderer.test.tsx` lock its current contract.
- **Conclusion**: Move the toolkit renderer to a feature-specific session-live renderer is wrong — the toolkit one is consumed by toolkit dashboard preview UX. The G5a renderer is a SIBLING surface; it consumes live session data (player roster + per-player `scoreData` + `viewerRole`), not toolkit template metadata.

### Existing surface to refactor / displace

- `apps/web/src/components/features/session-live/LiveScoringPanel.tsx` — hard-codes Points-only numeric scoreboard with `scores: ReadonlyArray<{ playerId, playerName, score, isWinner }>`. Used in:
  - `apps/web/src/components/features/session-live/index.ts:35`
  - `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:54, 848, 973`
  - `apps/web/src/components/features/session-live/MobileBody.tsx` (via `MobileTab='score'`)
- Legacy `apps/web/src/components/session/live/ScoreBoard.tsx` — Zustand-driven Points-only with host proposal approve/reject. Per epic note: `Points` + non-host → legacy `ScoreBoard`; host or non-`Points` → `PolymorphicScoreEditor`. **Decision below in §3 supersedes this branching.**

### Known gap (per epic #2354 G5a body)

- `apps/web/src/lib/stores/live-session-store.ts` (lines 13–67):
  - `PlayerInfo { id, name, isHost, isOnline }` — **no `displayName`**, **no `scoringType`** selector at store level.
  - `scores: Record<string, number>` — Points-only legacy shape; no per-variant payload.
  - T6 in the epic hardcodes 'Points' currently. Confirmed: `live-session-store.ts` has no `scoringType` field anywhere.
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx:340` builds `LiveSessionFixture.players` with `{ id, name, role, score, isOnline }` — also no `scoringType` / `displayName`.
- `apps/web/src/lib/session-live/session-live-visual-test-fixture.ts:74-81` — `LiveSessionFixturePlayer { id, name, role, score, isOnline }`.

### Conclusion

A new `apps/web/src/components/features/session-live/scoring/ScoringPanelRenderer.tsx` directory is needed:
- Distinct from toolkit renderer (different data contract).
- Wraps existing editor primitives for host-write mode.
- Renders a polymorphic **read-side display** for spectator/non-host (and the Points-fast-path retains legacy `LiveScoringPanel` behaviour for back-compat).

## §3 Architectural decisions

### D1 — New feature-local sub-package `session-live/scoring/`

Create:
```
apps/web/src/components/features/session-live/scoring/
  ScoringPanelRenderer.tsx        (top-level dispatcher)
  variants/
    PointsPanel.tsx               (live leaderboard + categories breakdown)
    RankingPanel.tsx              (ordered 1st/2nd/3rd with rank pills)
    BinaryWinPanel.tsx            (collective win/lose toggle)
    ObjectivesPanel.tsx           (checklist progress meter)
  ScoringPanelEmpty.tsx           (shared `Trophy` empty state)
  __tests__/
    ScoringPanelRenderer.test.tsx
    variants/{Points,Ranking,BinaryWin,Objectives}Panel.test.tsx
  index.ts
```

Rationale: keeps session-live a self-contained feature folder (matches `LiveScoringPanel`, `LiveAgentChat`, `PlayerRosterLive` siblings). Avoids polluting toolkit's `ScoringPanelRenderer` (different consumer, different schema). Re-uses the polymorphic editor primitives from `components/sessions/score-strategies/*` rather than reimplementing them.

### D2 — Renderer-vs-Editor dispatch boundary

The renderer's job is **layout + viewer adaptation**:
- Spectator / Player-viewing-other / Player-viewing-own-read-only → read-side display (variant panels with progress bars, ranks, checklists from the mockup).
- Host (or current Player when grant is `MIN_ROLE_PLAYER_ACTIONS` for own score in Points) → embed `PolymorphicScoreEditor` for that ScoreType, wired to `useUpdateSessionScores`.

This means `ScoringPanelRenderer` owns the **viewer/role gate** + **scoreType selector + dispatch**; `PolymorphicScoreEditor` remains the write primitive that the renderer composes when the role gate opens.

### D3 — Host-vs-Viewer dispatch rule

```
canEdit(viewerRole, scoringType) =
  viewerRole === 'Host'
  || (viewerRole === 'Player' && scoringType === 'Points')   // legacy carve-out; matches existing LiveScoringPanel
```

- `Spectator` → always read-only.
- `Host` → always editable (any ScoreType).
- `Player` + Points → can edit OWN score only (preserves current `LiveScoringPanel` Player+Host carve at line 62–74).
- `Player` + Ranking/BinaryWin/Objectives → read-only (mockup intent: these are host-resolved at game end).

### D4 — Backward compatibility with hardcoded Points

While the store gap (T6 hardcodes `'Points'`) is open, the renderer accepts a `scoringType?: ScoreType` prop with **default `'Points'`**. The orchestrator (`SessionLiveView`) keeps passing the current Points-flavour data via a thin adapter (see Task T7), so this PR ships incrementally without depending on the store schema migration.

A separate follow-up issue (recommended title: `chore(session-live): wire useLiveSessionStore.scoringType + displayName for G5a renderer`) tracks the store-shape gap (§6 risk R2).

### D5 — Token discipline

All variants use ONLY semantic tokens (`bg-card`, `bg-muted`, `border-border`, `text-foreground`, `text-muted-foreground`) + entity utilities (`bg-entity-toolkit/10`, `text-entity-session`, `ring-entity-event/30`). Avoid mockup's `var(--bg-card)` inline-style pattern. The leader gold accent uses `text-entity-toolkit` not `text-amber-*`.

### D6 — Data contract (DTO between orchestrator and renderer)

```ts
type ScoringPanelData =
  | { scoringType: 'Points';     payload: PointsPanelData     }
  | { scoringType: 'Ranking';    payload: RankingPanelData    }
  | { scoringType: 'BinaryWin'; payload: BinaryWinPanelData  }
  | { scoringType: 'Objectives'; payload: ObjectivesPanelData };
```

The renderer reads the discriminator (mirror of the editor's `ScoreChangePayload`) and dispatches. Read shape is a SIBLING discriminated union — see §5.

### D7 — i18n labels passed via `labels` prop

Mirror the existing `LiveScoringPanelLabels` convention (`SessionLiveView.tsx:627`): all strings come from `useTranslation` upstream, never raw ICU in the component. ICU plural is pre-resolved.

## §4 Task breakdown (TDD, test → impl → commit)

| # | WP | Task | Files | Type | Effort |
|---|---|---|---|---|---|
| T0 | WP0 | Plan land + scaffold barrel | `scoring/index.ts` | impl | 10m |
| T1 | WP1 | Type contract + dispatch table | `scoring/types.ts` | test+impl | 30m |
| T2 | WP2 | `PointsPanel` (read-side) | `variants/PointsPanel.{tsx,test.tsx}` | TDD | 1.5h |
| T3 | WP2 | `RankingPanel` (read-side) | `variants/RankingPanel.{tsx,test.tsx}` | TDD | 1.5h |
| T4 | WP2 | `BinaryWinPanel` (read-side) | `variants/BinaryWinPanel.{tsx,test.tsx}` | TDD | 1h |
| T5 | WP2 | `ObjectivesPanel` (read-side) | `variants/ObjectivesPanel.{tsx,test.tsx}` | TDD | 1h |
| T6 | WP3 | `ScoringPanelRenderer` dispatcher + role gate + empty | `ScoringPanelRenderer.{tsx,test.tsx}` | TDD | 1.5h |
| T7 | WP4 | Orchestrator wiring + Points fast-path adapter | `SessionLiveView.tsx`, `index.ts` | impl + integration test | 1h |
| T8 | WP5 | Visual-test fixture variants (per-ScoreType) | `session-live-visual-test-fixture.ts` (extend, do NOT mutate existing keys) | impl | 45m |
| T9 | WP5 | Storybook/Playwright smoke (4 baselines) | `tests/visual/session-live-scoring.spec.ts` | E2E | 1h |
| T10 | WP6 | Follow-up issue: store schema migration | (no code — github issue body draft only in this plan) | doc | 15m |

**Estimate**: ≈9.5h focused; with TDD ceremony + PR review buffer → **2 dev-days realistic** (revised down from 3-5; see §7).

### TDD detail per task

#### T1 — Type contract

- **Test**: Asserts `ScoringPanelData` discriminator switch is exhaustive (compile-time `never` in `default`). Asserts `PointsPanelData.players[number]` has `{ id, displayName, score, isWinner }`.
- **Impl**: define `ScoringPanelData` (see §5), re-export `ScoreType` from `score-strategies/types` to avoid drift.
- **Commit**: `feat(session-live): scoring renderer type contract + dispatch discriminator (T1)`

#### T2 — `PointsPanel`

- **Tests (TDD red first)**:
  - Renders ranked list desc by `score`.
  - First entry gets leader styling (`data-leader="true"` attr + `text-entity-toolkit` text class).
  - Renders score number with `tabular-nums`.
  - Optional `turnDelta` shows as `+N` with entity-toolkit accent.
  - Empty `players[]` → fallback to `ScoringPanelEmpty`.
  - Category breakdown table when `categories.length > 0`.
- **Commit**: `feat(session-live): PointsPanel read-side leaderboard with category breakdown (T2)`

#### T3 — `RankingPanel`

- **Tests**: Renders rank pill `1..N` (sorted by `rank` asc). Leader pill has `bg-entity-toolkit text-primary-foreground`. Trophy icon on rank=1. `sub` line under name. Empty → `ScoringPanelEmpty`.
- **Commit**: `feat(session-live): RankingPanel ordered list with rank pills (T3)`

#### T4 — `BinaryWinPanel`

- **Tests**: Collective outcome banner. Goal meter using entity-toolkit. Fail meter using `bg-entity-event`. Conditions list with weight badge.
- **Commit**: `feat(session-live): BinaryWinPanel collective outcome + meters (T4)`

#### T5 — `ObjectivesPanel`

- **Tests**: `Completati N/M` counter + progress meter. Each objective row: checkbox icon + label + optional `progress` mono text. Completed rows strikethrough.
- **Commit**: `feat(session-live): ObjectivesPanel checklist with progress meter (T5)`

#### T6 — `ScoringPanelRenderer` dispatcher (the centerpiece)

- **Tests**:
  - Renders `<ScoringPanelEmpty>` when `data == null`.
  - Switches to `<{Points,Ranking,BinaryWin,Objectives}Panel>` per discriminator.
  - **Host gate**: when `viewerRole === 'Host'`, embeds `PolymorphicScoreEditor` AND read-side panel.
  - **Player-Points carve-out**: when `viewerRole === 'Player'` AND `scoringType === 'Points'`, embeds Points editor scoped to own player.
  - **Spectator/Player-non-Points**: NO editor rendered.
  - Unknown scoreType: runtime fallback renders `ScoringPanelEmpty` with `data-score-type` debug attr.
  - `data-testid="scoring-panel"` + `data-score-type` + `aria-label="Scoring panel"` on root section.
- **Commit**: `feat(session-live): ScoringPanelRenderer polymorphic dispatch with host/viewer gate (T6, closes #2373)`

#### T7 — Orchestrator wiring + adapter

- **Tests**: Renders Points variant by default. Switching fixture variant via `?fixture=ranking` renders Ranking panel. Host fixture renders editor + panel.
- **Impl**: Build `ScoringPanelData` from current `scores` + `activeSession.players` (default `scoringType: 'Points'`). Replace `<LiveScoringPanel>` usages at lines 848 and 973 with `<ScoringPanelRenderer>`.
- **Commit**: `feat(session-live): SessionLiveView wires ScoringPanelRenderer with Points fast-path (T7)`

#### T8 — Visual-test fixtures per ScoreType

- Extend `session-live-visual-test-fixture.ts` with `VISUAL_TEST_FIXTURE_SESSION_RANKING`, `_BINARY_WIN`, `_OBJECTIVES`.
- **Commit**: `test(session-live): visual-test fixtures for 4 ScoreType variants (T8)`

#### T9 — Playwright visual smoke

- Add `tests/visual/session-live-scoring.spec.ts` taking baseline screenshots per variant.
- **Commit**: `test(session-live): Playwright visual baselines for ScoringPanelRenderer (T9)`

#### T10 — Follow-up issue for store schema gap

- Draft issue body in §6 R2 (do not file from this PR).
- **Commit**: `docs(superpowers): file follow-up #TBD for useLiveSessionStore.scoringType wiring (T10)`

## §5 Type contract — `ScoringPanelData` discriminated union

```ts
// apps/web/src/components/features/session-live/scoring/types.ts

import type {
  ScoreType,
  PointsScoreData,
  BinaryWinScoreData,
  ObjectivesScoreData,
  RankingScoreData,
  PlayerOption,
} from '@/components/sessions/score-strategies/types';

/** Read-side player view — mirrors mockup data.players shape. */
export interface ScoringPlayerView {
  readonly id: string;
  readonly displayName: string;
  /** Used by Points/Ranking variants. */
  readonly score?: number;
  /** Optional rank pill input (Ranking variant). */
  readonly rank?: number;
  /** Last-turn delta indicator (Points variant; mockup `turnDelta`). */
  readonly turnDelta?: number;
  /** Subtitle line under name (Ranking variant `sub`). */
  readonly sub?: string;
  /** Avatar hue (matches mockup MAI palette via entity tokens). */
  readonly hue?: number;
}

export interface PointsPanelData {
  readonly scoringType: 'Points';
  readonly players: ReadonlyArray<ScoringPlayerView>;
  readonly categories?: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly computation: 'Count' | 'Sum' | 'RankBased' | 'Custom';
    readonly description?: string;
  }>;
  readonly breakdown?: Readonly<Record<string, Readonly<Record<string, number>>>>;
  readonly editorData?: PointsScoreData;
}

export interface RankingPanelData {
  readonly scoringType: 'Ranking';
  readonly meta?: string;
  readonly ranking: ReadonlyArray<ScoringPlayerView & { readonly rank: number }>;
  readonly editorData?: RankingScoreData;
}

export interface BinaryWinPanelData {
  readonly scoringType: 'BinaryWin';
  readonly collective: {
    readonly goalLabel: string;
    readonly goalValue: number;
    readonly goalMax: number;
    readonly goalHint?: string;
    readonly failLabel: string;
    readonly failValue: number;
    readonly failMax: number;
    readonly failHint?: string;
  };
  readonly categories: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly computation: 'Count' | 'Sum' | 'RankBased' | 'Custom';
    readonly weight: number; // > 0 = win, < 0 = lose, 0 = neutral
    readonly description?: string;
  }>;
  readonly editorData?: BinaryWinScoreData;
}

export interface ObjectivesPanelData {
  readonly scoringType: 'Objectives';
  readonly meta?: string;
  readonly objectives: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly done: boolean;
    readonly progress?: string;
  }>;
  readonly editorData?: ObjectivesScoreData;
}

export type ScoringPanelData =
  | PointsPanelData
  | RankingPanelData
  | BinaryWinPanelData
  | ObjectivesPanelData;
```

### Dispatch table

| `data.scoringType` | Render component (read) | Render editor (write, host or own-Points) |
|---|---|---|
| `Points` | `PointsPanel` | `PolymorphicScoreEditor scoringType='Points'` |
| `Ranking` | `RankingPanel` | `PolymorphicScoreEditor scoringType='Ranking'` (host only) |
| `BinaryWin` | `BinaryWinPanel` | `PolymorphicScoreEditor scoringType='BinaryWin'` (host only) |
| `Objectives` | `ObjectivesPanel` | `PolymorphicScoreEditor scoringType='Objectives'` (host only; `availableObjectives` required) |
| `null` | `ScoringPanelEmpty` | — |

## §6 Risks + mitigations

### R1 — Backward compatibility with legacy `ScoreBoard`

- **Risk**: `apps/web/src/components/session/live/ScoreBoard.tsx` (Zustand-driven, Points-only with host approve/reject) — confirmed lives at `apps/web/src/app/(authenticated)/sessions/[id]/scoreboard/` (sister surface).
- **Mitigation**: ScoringPanelRenderer does NOT touch `session/live/ScoreBoard.tsx`. The "Points + non-host → legacy ScoreBoard" branching in epic body is **superseded**: with the renderer's role gate, the non-host Points path renders a fresh read-side `PointsPanel`. File a deprecation note in `session/live/ScoreBoard.tsx` header (Task T7 side-effect).

### R2 — `useLiveSessionStore` schema gap (`scoringType` + `displayName`)

- **Risk** (per epic): store has no `scoringType` selector and `PlayerInfo` lacks `displayName`. T6 in the epic hardcodes `'Points'` currently.
- **Mitigation A (this PR)**: Renderer accepts `ScoringPanelData` via props. The orchestrator (`SessionLiveView`) defaults `scoringType: 'Points'` and synthesises `displayName = playerName`. Renderer is SHIPPABLE without store changes.
- **Mitigation B (follow-up)**: File issue `chore(session-live): wire useLiveSessionStore.scoringType + displayName + per-variant scoreData payload`.

### R3 — Mockup fidelity vs token discipline

- **Risk**: Mockup uses `var(--bg-card)`, `var(--text-muted)`, inline style props with `eHsl('toolkit')`. ESLint `local/no-hardcoded-color-utility` rejects raw color utilities at error level.
- **Mitigation**: Map mockup CSS-var calls to semantic Tailwind tokens (`bg-card`, `text-foreground`, `border-border`, `text-entity-toolkit`, `bg-entity-session/10`, `text-entity-event`, `rounded-full`, `rounded-lg`, `font-display`, `font-mono`). Avatar's `linear-gradient(135deg, hsl(${p.hue}…))` — use CSS custom property `style={{ '--avatar-hue': p.hue }}` (allowed by ESLint when property name is `--avatar-*`).

### R4 — `PolymorphicScoreEditor` Objectives requires `availableObjectives`

- **Risk**: Editor throws when Objectives + missing list.
- **Mitigation**: `ObjectivesPanelData.objectives[]` carries `{ id, label }` — the renderer derives `availableObjectives = data.objectives.map(o => o.label)` before mounting the editor. Defensive guard test (T6).

### R5 — Double-source-of-truth between read panel and editor

- **Risk**: When host edits, the read-side panel could go stale until the mutation invalidates queries.
- **Mitigation**: `useUpdateSessionScores` already invalidates `['session', sessionId]` AND `['live-session', sessionId]`. Editor `onChange` updates local editor state; the renderer's read-side panel re-reads from props after invalidation refetch.

### R6 — `useUpdateSessionScores` 403 forbidden when viewer is non-host

- **Risk**: Player editing own Points triggers 403 because backend `UpdateSessionScoresCommand` IDOR-guards host-only.
- **Mitigation (in scope)**: For `Player + Points` carve-out, the renderer routes the score update through the **legacy** `PUT /api/v1/game-sessions/{id}/participants/{playerId}/score` endpoint (already wired in `SessionLiveView` `_handleScoreUpdate` at line 432), NOT through `useUpdateSessionScores`. The polymorphic mutation is host-only.

## §7 Effort estimate breakdown

| Bucket | Subtask | Hours |
|---|---|---|
| Type contract (T1) | discriminated union + dispatch table | 0.5 |
| 4 read-side variants (T2-T5) | TDD per panel | 5 (≈1.25 each) |
| Dispatcher + role gate (T6) | most complex; 8 test cases | 1.5 |
| Orchestrator wiring (T7) | adapter + replace LiveScoringPanel calls | 1 |
| Visual fixtures (T8) | 3 new fixtures + parse cases | 0.75 |
| Playwright baselines (T9) | 4 screenshots | 1 |
| Follow-up issue doc (T10) | doc only | 0.25 |
| **Pure dev sum** | | **~10h** |
| TDD red-cycle ceremony | +20% on TDD tasks | +1.5h |
| PR review + lint:tokens loop | | +1h |
| **Buffered** | | **~12.5h ≈ 1.5-2 dev days** |

### Revised vs original 3-5 day estimate

**Revised estimate: 2 dev-days.**

Rationale for revision down:
- BE shipped (no integration unknowns).
- `PolymorphicScoreEditor` shipped (no editor reimplementation).
- `useUpdateSessionScores` shipped with `UpdateSessionScoresError` tagged union.
- Mockup is concrete reference.
- Type contract aligns with existing `ScoreDataByType`.
- Existing toolkit `ScoringPanelRenderer` test file is a strong template to mirror.

Remaining risk to estimate:
- Token discipline ESLint may need ~1h to resolve gradient + dark-bg variants.
- Visual baselines require pixel-perfect chase + may add 1-2h.

## Critical Files for Implementation

- `apps/web/src/components/features/session-live/LiveScoringPanel.tsx` (the surface to displace)
- `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx` (write primitive to compose for host mode)
- `apps/web/src/components/sessions/score-strategies/types.ts` (source-of-truth ScoreType + ScoreDataByType + PlayerOption)
- `apps/web/src/hooks/use-update-session-scores.ts` (mutation hook + UpdateSessionScoresError tagged union)
- `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` (visual spec)
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` (orchestrator wiring point)
- `apps/web/src/components/toolkit/__tests__/ScoringPanelRenderer.test.tsx` (test template to mirror)
