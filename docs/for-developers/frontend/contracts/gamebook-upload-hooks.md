# `/gamebook/upload` Phase 0.5 Hook Composition Contract — SP6 Phase C

> **Status:** Phase 0.5 spec — pre-implementation contract for `/gamebook/upload` Tier L 3-step wizard v2 brownfield migration. Issue **#789**, plan §Phase C, parents Wave D.2 PR #749 + #753 (Foundation+Interactions Tier L+ blueprint), Wave D.3 PR #762 (5-task TDD pattern).
>
> **Scope:** mobile-first 3-step photo upload wizard replacing existing partial impl `apps/web/src/app/(authenticated)/gamebook/upload/`. Game selection → camera capture → indexing progress.
>
> **Out of scope (deferred):** real translation endpoint wiring (Phase 3 Task 3.5e), per-page confidence tracking backend (only batch-level avg available), premium tier purchase flow, BGG API audit if not exposed.

---

## §1. Overview

### Tier classification

`/gamebook/upload` is **Tier L** per plan classification. Drivers:

- **3 sub-routes via wizard step**: 1=game-select, 2=camera, 3=indexing — each with its own sub-FSM and side-effects
- **11-state FSM total** distributed across 3 steps (vs Wave D.2 Tier L+ 11-cell summary FSM)
- **2 production hooks** (`usePhotoBatchUpload` mutation + `usePhotoBatchStatus` polling) plus 2 capability adapters (camera + offline) — non-trivial cartesian
- **Real-time semantics**: polling cadence + retry budget ([1,2,4,8,16] = 31s) + camera permission state matrix + offline detection
- **Foundation+Interactions sub-PR split MANDATORY**: lesson from Wave D.2 PR #749 cherry-pick chain anti-pattern (AP10) — moving FSM during Interactions costs rebuilds, so Foundation owns full FSM, Interactions adds wiring only

### Dispatch strategy

Foundation+Interactions split (Fowler decision per plan §Phase C):

1. **Foundation sub-PR** (~5h) — read-only static-fixture impl: lib + 5 read-only components + orchestrator skeleton + E2E foundation + baselines bootstrap
2. **Interactions sub-PR** (~5h) — extends Foundation: camera/offline/confidence libs + 6 interactive components + orchestrator extension + integration tests + E2E smoke + bundle rebaseline

Both sub-PRs use **5-task TDD subagent dispatch SERIAL** per `feedback_subagent-serial-only.md`. Total ~165 unit + 25 integration + 22 E2E = ~212 tests.

### Parent issue + sub-PRs

- **Parent**: #789 (SP6 Phase C `/gamebook/upload` Tier L 3-step wizard)
- **Foundation child PR**: opened from `feature/issue-789-upload-foundation`
- **Interactions child PR**: opened from `feature/issue-789-upload-interactions` AFTER Foundation merged

### Pattern parents

- **Wave D.2 PR #749 + #753** — Tier L+ Foundation+Interactions blueprint (lazy dialogs + reducer composition + cancel-during-side-effect)
- **Wave D.3 PR #762** — 5-task TDD with subagent review (spec compliance + code quality)
- **Wave 4 D1 PR #717** — single-shot Tier S blueprint (reused for component-level patterns)
- **Wave C.1 PR #702** — Phase 0.5 sub-hook contract pattern (4-hook composition)

---

## §2. Route + 3-step FSM

### Route

```
/gamebook/upload?step=1|2|3 [&gameId=<uuid>] [&batchId=<uuid>] [&fixture=<kind>]
```

- `?step` = wizard step number (SSOT for step navigation)
- `?gameId` = selected game UUID (SSOT for step 1 → step 2 transition)
- `?batchId` = active batch UUID (SSOT for step 2 → step 3 transition; allows direct deep-link to step 3)
- `?fixture` = visual override gated by `STATE_OVERRIDE_ENABLED` env var (NEVER active in production build)

### 11-state FSM

State machine lives ENTIRELY in Foundation `apps/web/src/lib/gamebook-upload/fsm.ts`. Interactions never modifies cell shape — only adds side-effect handlers wired to FSM-defined transitions.

**Step 1 — Game selection (4 cells)**

| Cell | Trigger | Visible UI | Next |
|------|---------|------------|------|
| `step1-default` | `?step=1` no query | Catalog grid (5 games) + search bar (Catalogo/BGG tabs) + Continua CTA disabled until `?gameId` set | search input → `step1-searching` · select game → `?gameId=` set, Continua enabled |
| `step1-searching` | search input typed (debounce 250ms) | Catalog filtered + tab counter `(N)` | filter empty → `step1-no-results` · BGG tab clicked → `step1-bgg-loading` |
| `step1-no-results` | search query yields 0 catalog matches | Empty illustration + 3 ActionCards (Crea nuovo / Cerca BGG / Indicizza solo per me) | ActionCard tap → respective flow (BGG → `step1-bgg-loading`; create-new → modal; private → `?gameId=` synth) |
| `step1-bgg-loading` | BGG tab clicked OR ActionCard "Cerca su BGG" | Loading spinner + 3-row skeleton + tab badge | success → catalog grid w/ BGG results · timeout (>10s) → fallback empty state |

**Step 2 — Photo capture (5 cells)**

| Cell | Trigger | Visible UI | Next |
|------|---------|------------|------|
| `step2-ready` | `?step=2&gameId=` + camera `granted` + light OK | Viewfinder + frame green + light-meter ≥40% + shutter enabled | shutter tap → optimistic thumb-strip add, transitions back to `ready` (multi-shot loop) · ✓ done → `?step=3&batchId=` set |
| `step2-capturing` | mid-shot animation (≤500ms) | Shutter inner scale 0.92 + thumb optimistic spawn | settles → `step2-ready` (or `step2-low-light`/`step2-failed` based on next frame) |
| `step2-low-light` | light-meter <30% (heuristic) | Frame yellow + light-meter red + shutter disabled + hint "Avvicina alla luce" | light improves → `step2-ready` · user retreats → `step2-ready` |
| `step2-failed` | page-detection heuristic returns false (corner detection ≤2 of 4) | Frame red dashed + corner accents red + shutter disabled + hint "Bordi non rilevati" | frame valid → `step2-ready` |
| `step2-denied` | camera permission `denied` OR `unsupported` | NO viewfinder; full-bleed permission-denied illustration + "Apri impostazioni" CTA + "Carica dalla galleria" fallback (file picker `<input type="file" capture>`) | gallery upload → submits batch directly → `?step=3&batchId=` |

**Step 3 — Indexing progress (4 cells + 1 modal overlay)**

| Cell | Trigger | Visible UI | Next |
|------|---------|------------|------|
| `step3-progress` | `?step=3&batchId=` + status polling `Pending`/`Processing` | Progress bar + N/total counter + thumb grid w/ processing spinners + chip stats | status `Completed` → `step3-complete` · status `Failed` → `step3-partial` (retake low-conf) · network error 5s+ → `step3-offline` · cancel tap → `step3-cancel-modal` |
| `step3-partial` | status `Completed` AND ≥1 page conf<0.5 | Banner "Quasi pronto" + retake CTAs on low-conf thumbs + 2-button footer (Salta / Riscatta) | retake tap → re-enter step 2 with same batchId · skip → orchestrator finalizes + redirects |
| `step3-complete` | status `Completed` AND all conf≥0.5 | "Manuale pronto!" + ✓ counter + 2-button footer (Salva e basta / Inizia sessione) | session CTA → router.push(`/sessions/new?gameId=`) · save → router.push(`/gamebook?gameId=`) |
| `step3-offline` | `navigator.onLine === false` OR fetch failure ≥5s | Offline banner + frozen progress bar + processing thumbs paused + "Riprova ora" button + 31s retry-timer-overlay | online + retry success → `step3-progress` · 31s exhausted → `step3-failed-terminal` (separate manual-recovery state) |

**Modal overlay (additive, not standalone state)**:

| Cell | Trigger | Visible UI |
|------|---------|------------|
| `step3-cancel-modal` | user taps × close on step 2 OR step 3 with `capturedCount > 0` | Modal w/ ⚠️ icon + body "Hai già scattato N pagine. Se annulli ora, dovrai ricominciare." + 2 buttons (Continua / Sì, annulla) — focus trap + ESC handler — see Wave D.2 dialog focus trap pattern |

**Terminal cell**:

- `wizard-cancelled` — user confirmed cancel from `step3-cancel-modal` OR back-arrow on step 1 → router.replace(`/gamebook`) — NO further transitions

### Step transition diagram

```
URL state: ?step=1
  └─ step1-default ─[search]→ step1-searching ─[empty]→ step1-no-results
                              └─[BGG tab]→ step1-bgg-loading ─[results]→ step1-default
  └─ step1-* ─[gameId selected + Continua]→ ?step=2 ──┐
                                                       ▼
URL state: ?step=2&gameId=<uuid>
  └─ step2-ready ─[shutter]→ step2-capturing ─[settled]→ step2-ready (multi-shot loop)
                  ├─[low light]→ step2-low-light ─[improved]→ step2-ready
                  └─[detection fail]→ step2-failed ─[frame valid]→ step2-ready
  └─ step2-denied (terminal-step2; gallery upload skips to ?step=3)
  └─ ✓done OR gallery → ?step=3&batchId=<uuid> ──┐
                                                  ▼
URL state: ?step=3&batchId=<uuid>
  └─ step3-progress ─[Completed all conf≥0.5]→ step3-complete (terminal)
                     ├─[Completed AND any conf<0.5]→ step3-partial ─[retake]→ ?step=2&batchId
                     ├─[network err ≥5s]→ step3-offline ─[retry success]→ step3-progress
                     │                                    └─[31s exhausted]→ step3-failed-terminal
                     └─[user × cancel]→ step3-cancel-modal (overlay) ─[confirm]→ wizard-cancelled
```

---

## §3. Hook composition matrix

### Hook inventory

| Hook | Status | Origin | Purpose | Sub-PR |
|------|--------|--------|---------|--------|
| `usePhotoBatchUpload` | EXISTING | `apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts` | Mutation: POST /api/v1/gamebook/{gameId}/photos | Interactions (extended w/ idempotency-key) |
| `usePhotoBatchStatus` | EXISTING | `apps/web/src/lib/gamebook/hooks/usePhotoBatchStatus.ts` | Polling query: GET /api/v1/gamebook/{gameId}/photos/{batchId}/status | Interactions (extended w/ degraded-polling) |
| `useGames` | EXISTING | `apps/web/src/hooks/queries/useGames.ts` | Catalog query for step 1 | Foundation (read-only) |
| `useSearchBggGames` | EXISTING | `apps/web/src/hooks/queries/useSearchBggGames.ts` | BGG search for step 1 | Foundation (read-only) |
| `useCameraCapabilities` | NEW | `apps/web/src/lib/gamebook-upload/camera-capabilities.ts` | Permission state matrix + getUserMedia wrapper | Interactions |
| `useOfflineBudget` | NEW | `apps/web/src/lib/gamebook-upload/offline-budget.ts` | [1,2,4,8,16] retry timer + AbortController + cancel | Interactions |
| `useGamebookUploadOrchestrator` | NEW | `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` (inline) | Composes all of the above + URL state SSOT + 11-state FSM transitions | Interactions (Foundation skeleton only) |

### Composition tree

```
GamebookUploadView (orchestrator)
├── useUrlState (read ?step / ?gameId / ?batchId / ?fixture)
├── useGamebookUploadFSM (Foundation pure helper — derives cell from URL + hook results)
│
├── [Step 1] useGames + useSearchBggGames (Foundation)
│   └── derives → step1-default | step1-searching | step1-no-results | step1-bgg-loading
│
├── [Step 2] useCameraCapabilities (Interactions)
│   ├── permissionState: 'granted' | 'denied' | 'prompt' | 'unsupported'
│   ├── lightMeterValue: number 0..1 (heuristic from MediaStream pixel sampling)
│   ├── pageDetectionScore: number 0..1 (corner heuristic)
│   └── derives → step2-ready | step2-low-light | step2-failed | step2-denied
│
└── [Step 3] usePhotoBatchUpload + usePhotoBatchStatus + useOfflineBudget (Interactions)
    ├── upload mutation triggered on step 2 ✓done → batchId set
    ├── status polling 2s cadence until terminal
    ├── retry budget kicks on network err ≥5s
    └── derives → step3-progress | step3-partial | step3-complete | step3-offline | step3-cancel-modal | step3-failed-terminal
```

### Foundation vs Interactions ownership

**Foundation owns**:
- Full 11-state FSM (`fsm.ts` cells + transitions)
- Schemas (`schemas.ts` Zod)
- Visual fixture (`visual-test-fixture.ts` 11+ kinds)
- Step 1 read-only logic (game search + BGG search consumed read-only via existing hooks)
- Skeleton orchestrator that handles `?fixture=` override only (no real camera, no real upload)
- 5 read-only components (StepIndicator, GameSearchBar, GameCard, NoResultsPanel, ActionCard)
- 3 E2E specs (visual-migrated/v2-states/a11y) for static fixture rendering

**Interactions owns**:
- Camera capabilities adapter (`camera-capabilities.ts`)
- Offline budget reducer (`offline-budget.ts`)
- Confidence classifier (`confidence-classifier.ts`)
- 6 interactive components (CameraViewfinder, PageThumb, ConfidenceBadge, OfflineBanner, CancelModal, DesktopDropFallback)
- Orchestrator EXTENSION wiring side-effects to existing FSM cells (NEVER moves FSM)
- Integration tests (~25 MSW + TanStack)
- E2E smoke + camera permission denied + offline retry + cancel modal focus trap

**Anti-pattern (forbidden)**: moving cell shape or transition logic from Foundation `fsm.ts` to Interactions during sub-PR. If transition needs to change, MUST go back to Foundation as a fix-up commit. Per Wave D.2 PR #749 lesson AP10.

---

## §4. Schema contract

### Zod schemas (lib/gamebook-upload/schemas.ts)

```ts
import { z } from 'zod';

// ─────────────────────────────────────────────────────────────
// Step 1 — Game selection
// ─────────────────────────────────────────────────────────────

export const GameSearchTabSchema = z.enum(['catalog', 'bgg']);
export type GameSearchTab = z.infer<typeof GameSearchTabSchema>;

export const CatalogGameRefSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(1),
  publisher: z.string().nullable(),
  coverImageUrl: z.string().url().nullable(),
  sharedByCount: z.number().int().nonnegative(),
  isIndexed: z.boolean(),
});
export type CatalogGameRef = z.infer<typeof CatalogGameRefSchema>;

export const BggSearchResultSchema = z.object({
  bggId: z.number().int().positive(),
  title: z.string().min(1),
  publisher: z.string().nullable(),
  yearPublished: z.number().int().nullable(),
});
export type BggSearchResult = z.infer<typeof BggSearchResultSchema>;

export const NoResultsActionSchema = z.enum([
  'create-new',
  'search-bgg',
  'index-private',
]);
export type NoResultsAction = z.infer<typeof NoResultsActionSchema>;

// ─────────────────────────────────────────────────────────────
// Step 2 — Camera capture
// ─────────────────────────────────────────────────────────────

export const CameraPermissionStateSchema = z.enum([
  'granted',     // full UX: native viewfinder
  'denied',      // fallback to file picker (≤500ms inline swap)
  'prompt',      // initial state, show CTA
  'unsupported', // no mediaDevices.getUserMedia → fallback to file picker
]);
export type CameraPermissionState = z.infer<typeof CameraPermissionStateSchema>;

export const LightMeterReadingSchema = z.object({
  value: z.number().min(0).max(1),       // 0..1 normalized brightness
  level: z.enum(['too-dark', 'low', 'medium', 'ok']),
});
export type LightMeterReading = z.infer<typeof LightMeterReadingSchema>;

export const CapturedPageSchema = z.object({
  pageNumber: z.number().int().positive(),
  /** Object URL for client-side preview thumbnail (revoked on unmount) */
  thumbObjectUrl: z.string(),
  /** Optimistic state before backend confirmation */
  pendingUpload: z.boolean(),
});
export type CapturedPage = z.infer<typeof CapturedPageSchema>;

// ─────────────────────────────────────────────────────────────
// Step 3 — Indexing progress
// ─────────────────────────────────────────────────────────────

export const ConfidenceLevelSchema = z.enum(['high', 'medium', 'low']);
export type ConfidenceLevel = z.infer<typeof ConfidenceLevelSchema>;

/**
 * Per-page metadata derived from batch status + heuristic.
 * Backend currently exposes only batch-level avg — per-page conf inferred from
 * ordering + processing flag until backend tracks per-page (deferred).
 */
export const IndexedPageMetaSchema = z.object({
  pageNumber: z.number().int().positive(),
  /** null while processing */
  confidence: ConfidenceLevelSchema.nullable(),
  /** True while OCR pipeline still running this page */
  isProcessing: z.boolean(),
  /** True if conf=low and user prompted to retake */
  retakeRequested: z.boolean(),
});
export type IndexedPageMeta = z.infer<typeof IndexedPageMetaSchema>;

// ─────────────────────────────────────────────────────────────
// Idempotency-key composition (sent on every upload request)
// ─────────────────────────────────────────────────────────────

/**
 * Composes deterministic idempotency-key for batch retries.
 * Server-side dedup based on full string match.
 * Format: `${batchId}:${pageNumber}:${attemptCount}`
 */
export function composeIdempotencyKey(
  batchId: string,
  pageNumber: number,
  attemptCount: number
): string {
  return `${batchId}:${pageNumber}:${attemptCount}`;
}

// ─────────────────────────────────────────────────────────────
// Offline retry budget
// ─────────────────────────────────────────────────────────────

export const RETRY_DELAYS_MS = [1_000, 2_000, 4_000, 8_000, 16_000] as const;
export const RETRY_BUDGET_TOTAL_MS = RETRY_DELAYS_MS.reduce((a, b) => a + b, 0); // = 31_000

export const RetryStateSchema = z.object({
  attemptCount: z.number().int().min(0).max(5),
  /** ms until next retry fires; null if idle/exhausted */
  nextRetryInMs: z.number().int().nullable(),
  isExhausted: z.boolean(),
});
export type RetryState = z.infer<typeof RetryStateSchema>;
```

### Backend reuse (existing schemas)

`lib/gamebook/schemas.ts` already exposes:
- `PhotoUploadItemSchema`, `UploadPhotoBatchRequestSchema`, `UploadPhotoBatchResponseSchema`
- `PhotoBatchStatusSchema`, `BATCH_TERMINAL_STATUSES`, `isBatchTerminal()`, `batchProgressPercent()`
- `ParagraphSchema` (deferred — translate flow only)

These are reused as-is. New schemas live in `lib/gamebook-upload/schemas.ts` to keep gamebook (consumption) decoupled from gamebook-upload (production).

---

## §5. 11 component specs

All components live under `apps/web/src/components/v2/gamebook-upload/`. Each exports default + named typed props, has `data-slot="gamebook-upload-<component>"` for E2E targeting + a11y exclusion scoping.

### 5.1 StepIndicator

**Owner**: Foundation
**Path**: `components/v2/gamebook-upload/StepIndicator.tsx`

```ts
interface StepIndicatorProps {
  current: 1 | 2 | 3;
  /** Optional back navigation handler (omits arrow if undefined) */
  onBack?: () => void;
  /** Disabled steps (cannot navigate forward to incomplete prereqs) */
  unlockedSteps: ReadonlyArray<1 | 2 | 3>;
}
```

**States**: 1-active, 2-active, 3-active. Each step renders done (✓) / active (number outlined) / pending (number muted).

**A11y**: `role="navigation"` + `aria-label="Wizard progress"`. Active step has `aria-current="step"`. Done steps include screenreader-only "completed" text.

**Visual**: orange (entityHsl game) accent for done/active circles + connecting line. Uses MeepleAI design tokens, no hardcoded colors.

### 5.2 GameSearchBar

**Owner**: Foundation
**Path**: `components/v2/gamebook-upload/GameSearchBar.tsx`

```ts
interface GameSearchBarProps {
  value: string;
  onChange: (value: string) => void;
  activeTab: GameSearchTab;
  onTabChange: (tab: GameSearchTab) => void;
  catalogCount: number;
  bggCount: number;
  /** Optional autoFocus on mount (only when ?step=1 and no ?gameId) */
  autoFocus?: boolean;
}
```

**States**: idle (placeholder visible), focused (outline orange), with-value (clear button visible). Tab badges show counts with `(N)` suffix.

**A11y**: `<input type="search" role="searchbox">` + `aria-label`. Tabs use `role="tablist"` + `role="tab"` + `aria-controls` + `aria-selected`. Debounce 250ms before triggering parent.

### 5.3 GameCard

**Owner**: Foundation
**Path**: `components/v2/gamebook-upload/GameCard.tsx`

```ts
interface GameCardProps {
  game: CatalogGameRef | BggSearchResult;
  selected: boolean;
  onClick: () => void;
  /** When 'bgg', render with kb-purple accent vs game-orange */
  source: 'catalog' | 'bgg';
}
```

**MeepleCard divergence**: Cards diverge from MeepleCard primitive (Gate C documented). Reasons:
- Mockup specifies `4/3` aspect ratio cover with linear-gradient background + emoji centered (no image asset support in v1)
- Selected state has `box-shadow: 0 0 0 4px <orange>/.15` ring effect not exposed by MeepleCard
- `sharedByCount` displayed inline with 👥 icon — MeepleCard doesn't expose this stat slot

Decision: **bespoke component** per Wave D.1 cards-diverge-from-MeepleCard pattern (PR #736). Documented in §13 Gate C.

**A11y**: `<button type="button">` (not link — selection action, not navigation). `aria-pressed={selected}`. Cover image has `aria-hidden` (decorative emoji).

### 5.4 NoResultsPanel

**Owner**: Foundation
**Path**: `components/v2/gamebook-upload/NoResultsPanel.tsx`

```ts
interface NoResultsPanelProps {
  searchQuery: string;
  onAction: (action: NoResultsAction) => void;
}
```

**Layout**: vertical stack — illustration (telescope emoji 🔭) + title `"{query}" non trovato` + 3 ActionCards.

**A11y**: `<section aria-label="No search results">`. Illustration has `aria-hidden`.

### 5.5 ActionCard

**Owner**: Foundation
**Path**: `components/v2/gamebook-upload/ActionCard.tsx`

```ts
interface ActionCardProps {
  icon: ReactNode;
  iconEntity: 'game' | 'kb' | 'agent'; // for accent color
  title: string;
  subtitle: string;
  onClick: () => void;
  badge?: string; // e.g. "G1.5"
}
```

**Visual**: horizontal card with leading icon (entity color background) + title + subtitle + optional Gherkin badge.

**A11y**: `<button type="button">`. Icon has `aria-hidden` (decorative emoji). Badge has `aria-label="Acceptance criterion {badge}"`.

### 5.6 CameraViewfinder

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/CameraViewfinder.tsx`

```ts
interface CameraViewfinderProps {
  permissionState: CameraPermissionState;
  lightReading: LightMeterReading;
  pageDetected: boolean; // false → red dashed frame
  capturedCount: number;
  capturedThumbs: ReadonlyArray<CapturedPage>;
  onShutter: () => void;
  onClose: () => void;       // triggers cancel-modal if capturedCount > 0
  onDone: () => void;        // disabled if capturedCount === 0
  onGalleryFallback: () => void; // file picker fallback
}
```

**States**: ready (green frame + light ok + shutter enabled) · low-light (yellow frame + light red + shutter disabled) · detection-failed (red dashed + corner accents red) · capturing (mid-shot, ≤500ms transition).

**A11y**: `<section role="region" aria-label="Camera viewfinder">`. Shutter button = `<button type="button" aria-label="Scatta foto">`. Light-meter has `role="meter"` + `aria-valuenow={value}` + `aria-valuemin={0}` + `aria-valuemax={1}`. Captured strip = `<ul role="list">`. Hint badge has `role="status"` + `aria-live="polite"`.

**Reduced motion**: shutter inner-scale animation disabled when `prefers-reduced-motion: reduce`.

### 5.7 PageThumb

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/PageThumb.tsx`

```ts
interface PageThumbProps {
  meta: IndexedPageMeta;
  onRetake?: () => void; // only when retakeRequested=true
}
```

**States**: processing (spinner overlay) · indexed-high (✓ green badge) · indexed-medium (◐ amber badge) · indexed-low (⚠ red badge + retake CTA).

**A11y**: `<li>` (children of `<ul role="list">` in indexing grid). Confidence badge has `aria-label={`Pagina ${pageNumber} confidenza ${confidenceText}`}`. Retake button has explicit `aria-label`.

### 5.8 ConfidenceBadge

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/ConfidenceBadge.tsx`

```ts
interface ConfidenceBadgeProps {
  level: ConfidenceLevel;
  size?: 'sm' | 'md';
}
```

**Visual**: circular badge with semantic color (green/amber/red) + glyph (✓/◐/⚠).

**A11y**: `<span role="img" aria-label={confidenceText}>`. Color is supplemental (glyph-bearing) — passes WCAG 1.4.1 use-of-color.

### 5.9 OfflineBanner

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/OfflineBanner.tsx`

```ts
interface OfflineBannerProps {
  retryState: RetryState;
  onManualRetry: () => void;
  onCancel: () => void;
}
```

**Visual**: full-width banner with 📡 icon + title "Connessione persa" + countdown text "Tentativo {n}/5..." + linear progress bar showing 31s budget elapsed + 2 buttons (Riprova ora / Annulla).

**A11y**: `<aside role="alert" aria-live="assertive">` (network failures are critical). Progress bar = `role="progressbar"` + `aria-valuenow` + `aria-valuemax`. Buttons explicit labels.

### 5.10 CancelModal

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/CancelModal.tsx`

```ts
interface CancelModalProps {
  open: boolean;
  capturedCount: number;
  onConfirmCancel: () => void;
  onContinue: () => void;
}
```

**Implementation**: Radix `Dialog` primitive (already present in shadcn/ui). Lazy-loaded via `React.lazy()` per bundle budget per Wave D.2 dialog pattern.

**Focus trap + ESC**: Wave D.2 ESC behavior matrix:
- ESC = invoke `onContinue()` (default cancel-cancel)
- Click outside (overlay) = invoke `onContinue()`
- Confirm button (Sì, annulla) = invoke `onConfirmCancel()` → FSM → `wizard-cancelled`
- Continue button = invoke `onContinue()`

Initial focus on Continue button (safe default — destructive action requires deliberate tab-then-click).

**A11y**: `aria-labelledby` to title + `aria-describedby` to body text. `role="alertdialog"` (destructive). Inert background while open.

### 5.11 DesktopDropFallback

**Owner**: Interactions
**Path**: `components/v2/gamebook-upload/DesktopDropFallback.tsx`

```ts
interface DesktopDropFallbackProps {
  onFilesDropped: (files: File[]) => void;
  /** Optional max files (default 50 per backend constraint) */
  maxFiles?: number;
}
```

**Trigger**: viewport ≥1024px (desktop) AND `?step=2` — replaces CameraViewfinder. Dashed orange border drop zone + "Trascina le foto qui" + "Sfoglia file" button + supporting text "JPG/PNG/HEIC · Max 50 file" + footer link "Invia link al telefono".

**A11y**: `<div role="region" aria-label="File drop zone">`. File picker `<input type="file" multiple accept="image/jpeg,image/png,image/heic" capture="environment">`. Drag-over visual feedback via `data-dragging` attribute + reduced-motion safe.

---

## §6. URL state SSOT

### URL params

| Param | Purpose | When set | When read |
|-------|---------|----------|-----------|
| `?step=1\|2\|3` | Wizard step indicator | `router.replace()` on step transition | orchestrator first paint + back/forward nav |
| `?gameId=<uuid>` | Selected game (step 1 → 2 transition) | `router.replace()` on game selection or deep-link | step 2 mount (camera target) + step 3 query key |
| `?batchId=<uuid>` | Active upload batch (step 2 → 3 transition) | `router.replace()` after `usePhotoBatchUpload` success | step 3 polling target + deep-link |
| `?fixture=<kind>` | Visual override (DEV/CI only) | NEVER in production runtime | Foundation skeleton + Interactions when `STATE_OVERRIDE_ENABLED===true` |

### Fixture kinds

```ts
type FixtureKind =
  | 'step1-default' | 'step1-searching' | 'step1-no-results' | 'step1-bgg-loading'
  | 'step2-ready' | 'step2-low-light' | 'step2-failed' | 'step2-denied' | 'step2-capturing'
  | 'step3-progress' | 'step3-partial' | 'step3-complete' | 'step3-offline' | 'step3-cancel-modal'
  // Light-meter standalone (interactions tests)
  | 'lightmeter-well-lit' | 'lightmeter-low-light' | 'lightmeter-too-dark';
```

### Fixture gating

```ts
// lib/gamebook-upload/visual-test-fixture.ts
export const STATE_OVERRIDE_ENABLED =
  process.env.NODE_ENV !== 'production' ||
  process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';
```

CI workflow `.github/workflows/visual-regression-migrated.yml` sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build` so the visual snapshot job has a working fixture path. Production builds (no env var) DCE the entire fixture branch via dead-code-elimination.

### URL transitions (no useState mirror)

Per Wave D.1/D.3 pattern, URL is **single source of truth** for step navigation. NO mirrored React state:

```tsx
// Orchestrator
const searchParams = useSearchParams();
const step = parseStepParam(searchParams.get('step')); // Foundation helper
const gameId = searchParams.get('gameId');
const batchId = searchParams.get('batchId');

// Transition example (game selected on step 1):
const onGameSelect = (gameId: string) => {
  router.replace(`/gamebook/upload?step=2&gameId=${gameId}`, { scroll: false });
};
```

---

## §7. Bundle budget

### Phase C target: **≤+120 KB** (Foundation ~50 + Interactions ~70)

| Sub-PR | Target | Components | Lib | Notes |
|--------|--------|-----------|-----|-------|
| Foundation | ~50 KB | StepIndicator + GameSearchBar + GameCard + NoResultsPanel + ActionCard (5 components) | fsm.ts + schemas.ts + visual-test-fixture.ts + index.ts | No camera, no upload, no animation libs |
| Interactions | ~70 KB | CameraViewfinder + PageThumb + ConfidenceBadge + OfflineBanner + CancelModal (lazy) + DesktopDropFallback (6 components) | camera-capabilities.ts + offline-budget.ts + confidence-classifier.ts + integration tests | CancelModal `React.lazy()` ⇒ split chunk |

### Mitigation if exceeded

- **CancelModal lazy loading**: `const CancelModal = React.lazy(() => import('./CancelModal'))` — only loads when user taps × close. Saves ~15 KB initial bundle.
- **DesktopDropFallback lazy**: only loads when viewport ≥1024px detected via `useMediaQuery` hook. Saves ~10 KB on mobile.
- **Confidence classifier separate chunk**: `confidence-classifier.ts` only used in step 3 — code-split via dynamic import in `useGamebookUploadOrchestrator`.

### Rebaseline policy

If post-Interactions merge `apps/web/bundle-size-baseline.json` exceeds ~+170 KB SP6 umbrella budget (Phase A 5 + Phase B 45 + Phase C 120 = 170 KB), open separate `chore(bundle-size)` PR per Wave D.2 PR #754 pattern. **Never merge Phase C with budget exceeded** — block on Gate D bootstrap-then-merge.

---

## §8. i18n keys

~50 keys per locale under `gamebook.upload.*` namespace. Symmetric IT/EN per AC-7. ICU plural for page counts.

### Key tree (sketch)

```
gamebook.upload
├── meta
│   ├── title: "Carica manuale | MeepleAI"
│   └── description: "Fotografa il tuo manuale di gioco passo dopo passo"
├── stepIndicator
│   ├── back: "Indietro"
│   ├── steps.game: "Gioco"
│   ├── steps.photo: "Foto"
│   └── steps.index: "Indice"
├── step1
│   ├── title: "Quale gioco vuoi indicizzare?"
│   ├── subtitle: "Scegli dal catalogo condiviso o cercane uno nuovo."
│   ├── searchPlaceholder: "Cerca un gioco..."
│   ├── tabCatalog: "Catalogo"
│   ├── tabBgg: "BGG"
│   ├── tabBadge: "{count}"  // ICU plural variants for catalog/BGG counts
│   ├── continueButton: "Continua"
│   ├── searchInfo: "Nessun risultato nel catalogo. Cerca su BGG ↑"
│   ├── bggLoading.title: "Cerco su BoardGameGeek..."
│   ├── bggLoading.subtitle: "Può richiedere qualche secondo"
│   ├── noResults.title: "\"{query}\" non trovato"
│   ├── noResults.subtitle: "Tre modi per continuare:"
│   ├── noResults.action.createNew.title: "Crea gioco nuovo"
│   ├── noResults.action.createNew.subtitle: "Manuale che non esiste in nessun database"
│   ├── noResults.action.bgg.title: "Cerca su BoardGameGeek"
│   ├── noResults.action.bgg.subtitle: "Fonte ufficiale con metadati completi"
│   ├── noResults.action.private.title: "Indicizza solo per me"
│   └── noResults.action.private.subtitle: "Privato — non condiviso con la community"
├── step2
│   ├── camera.aria: "Fotocamera mirino"
│   ├── camera.shutter.aria: "Scatta foto"
│   ├── camera.close.aria: "Chiudi fotocamera"
│   ├── camera.gallery.aria: "Apri galleria"
│   ├── camera.done.aria: "Fine — vai a indicizzazione"
│   ├── camera.counter: "{count} / {max}"  // ICU plural per language
│   ├── camera.hint.ready: "✓ Pagina riconosciuta"
│   ├── camera.hint.lowLight: "☁️ Avvicina alla luce"
│   ├── camera.hint.failed: "✗ Bordi non rilevati"
│   ├── camera.lightMeter.aria: "Misuratore luce"
│   ├── camera.lightMeter.low: "BASSA"
│   ├── camera.lightMeter.medium: "MEDIA"
│   ├── camera.lightMeter.ok: "OK"
│   ├── denied.title: "Camera bloccata"
│   ├── denied.subtitle: "MeepleAI non ha accesso alla fotocamera. Abilita i permessi nelle impostazioni del sistema."
│   ├── denied.openSettings: "Apri impostazioni"
│   ├── denied.gallery: "Carica dalla galleria"
│   ├── denied.hint: "💡 In alternativa, puoi caricare foto già scattate."
│   ├── desktopDrop.title: "Trascina le foto qui"
│   ├── desktopDrop.subtitle: "Su desktop puoi anche caricare foto già scattate."
│   ├── desktopDrop.formats: "Formati supportati: JPG, PNG, HEIC · Max {max} file"
│   ├── desktopDrop.browse: "Sfoglia file"
│   └── desktopDrop.sendToPhone: "Invia link al telefono"
├── step3
│   ├── inProgress.title: "Indicizzazione…"
│   ├── inProgress.subtitle: "Puoi tenere il telefono in tasca."
│   ├── partial.title: "Quasi pronto"
│   ├── partial.subtitle: "{ok} pagine ok, {retake} da riscattare per qualità migliore."
│   ├── complete.title: "Manuale pronto!"
│   ├── complete.subtitle: "{total} pagine indicizzate, {high} ad alta confidenza."
│   ├── offline.title: "Connessione persa"
│   ├── offline.subtitle: "Riprenderà automaticamente al ripristino."
│   ├── offline.retryNow: "Riprova ora"
│   ├── offline.attempt: "Tentativo {current}/{max}..."
│   ├── pagesCount: "{count, plural, one {# pagina} other {# pagine}}"  // ICU plural symmetric IT/EN
│   ├── confidence.high: "Alta"
│   ├── confidence.medium: "Media"
│   ├── confidence.low: "Bassa"
│   ├── confidence.processing: "In elaborazione"
│   ├── retake.aria: "Riscatta pagina {pageNumber}"
│   ├── retake.label: "📷 Riscatta"
│   ├── footer.savePlain: "Salva e basta"
│   ├── footer.startSession: "🎯 Inizia sessione"
│   ├── footer.skipPartial: "Salta — uso così"
│   └── footer.retakeBatch: "📷 Riscatta {count} pagine"  // ICU plural
└── cancelModal
    ├── title: "Annullare l'indicizzazione?"
    ├── body: "Hai già scattato {count, plural, one {# pagina} other {# pagine}}. Se annulli ora, dovrai ricominciare da capo."
    ├── continue: "Continua a indicizzare"
    └── confirm: "Sì, annulla"
```

ICU plural keys symmetric per AC-7:
- IT: `one`, `other` (Italian has 2 plural categories)
- EN: `one`, `other` (English has 2 plural categories)

Foundation sub-PR adds keys for steps 1-3 read-only states. Interactions sub-PR adds camera/offline/cancel-modal keys.

---

## §9. Camera contract

### 4-permission-state matrix (MANDATORY)

| State | UX | Trigger | Fallback |
|-------|----|---------|---------|
| `granted` | Full UX with native viewfinder + light-meter + page-detection | `permissions.query({name: 'camera'})` returns `granted` OR `getUserMedia()` succeeds | None — happy path |
| `denied` | Inline UI swap to file picker `<input type="file" capture="environment">` within ≤500ms — NO modal interruption | `permissions.query()` returns `denied` OR `getUserMedia()` rejects with `NotAllowedError` | `step2-denied` cell with "Apri impostazioni" CTA + gallery upload button |
| `prompt` | Initial CTA "Allow camera access" + button triggering `getUserMedia()` | `permissions.query()` returns `prompt` (typical first visit) | If user accepts → transitions to `granted` · If denies → transitions to `denied` |
| `unsupported` | Hide camera CTA entirely, fallback to file picker | `mediaDevices.getUserMedia` undefined (e.g., http://localhost without HTTPS, older browsers) | `step2-denied` cell with file picker visible, but "Apri impostazioni" CTA hidden |

### Permission detection flow

```ts
// lib/gamebook-upload/camera-capabilities.ts
export async function detectCameraPermissionState(): Promise<CameraPermissionState> {
  if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
    return 'unsupported';
  }
  // Permissions API not universal — fall back to feature-detect via getUserMedia
  if (typeof navigator.permissions?.query === 'function') {
    try {
      const status = await navigator.permissions.query({ name: 'camera' as PermissionName });
      return status.state as CameraPermissionState;
    } catch {
      // Some browsers throw on 'camera' query — fall through
    }
  }
  // Optimistic: assume 'prompt' until first user-initiated getUserMedia
  return 'prompt';
}

export async function requestCameraStream(): Promise<MediaStream | null> {
  if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
    return null; // unsupported
  }
  try {
    return await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'environment' },
      audio: false,
    });
  } catch (err) {
    if ((err as DOMException).name === 'NotAllowedError') {
      return null; // denied
    }
    throw err;
  }
}
```

### Playwright E2E mock strategy

**Chromium (primary E2E target)**:

```ts
// playwright.config.ts (Chromium project)
projects: [
  {
    name: 'chromium-camera',
    use: {
      ...devices['Desktop Chrome'],
      launchOptions: {
        args: [
          '--use-fake-ui-for-media-stream',         // bypass permission prompt
          '--use-fake-device-for-media-stream',     // synthetic 1280x720 video stream
          // optional: --use-file-for-fake-video-capture=/path/to/test-video.y4m
        ],
      },
    },
  },
]
```

**Webkit/Firefox parity**: `MediaStream` global mock pattern (overrides `navigator.mediaDevices.getUserMedia` via Playwright `addInitScript`):

```ts
// e2e/helpers/camera-mock.ts
export async function mockMediaStream(page: Page) {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'mediaDevices', {
      value: {
        getUserMedia: () =>
          Promise.resolve(new MediaStream()), // empty stream — UI must handle
      },
      configurable: true,
    });
  });
}
```

### Light-meter deterministic input

Real video stream sampling is non-deterministic in CI. Foundation+Interactions decouple via fixture mode:

```ts
// lib/gamebook-upload/camera-capabilities.ts
export function readLightMeter(stream: MediaStream | null, fixtureKind?: string): LightMeterReading {
  // Fixture mode (CI/visual tests)
  if (fixtureKind?.startsWith('lightmeter-')) {
    const map = {
      'lightmeter-well-lit': { value: 0.78, level: 'ok' as const },
      'lightmeter-low-light': { value: 0.30, level: 'low' as const },
      'lightmeter-too-dark': { value: 0.05, level: 'too-dark' as const },
    };
    return map[fixtureKind] ?? map['lightmeter-too-dark'];
  }
  // Real mode (browser): sample MediaStream pixel data via canvas
  if (!stream) return { value: 0, level: 'too-dark' };
  // Implementation: draw frame to canvas, sample 16×16 grid, compute avg luminance
  // ...
}
```

URL override `?fixture=lightmeter-well-lit` (gated by `STATE_OVERRIDE_ENABLED`) injects deterministic readings for E2E tests.

### Page-detection deterministic input

Same fixture pattern for corner-detection heuristic:

```ts
export function readPageDetectionScore(stream: MediaStream | null, fixtureKind?: string): number {
  if (fixtureKind === 'step2-failed') return 0.2;  // <0.5 threshold → failed
  if (fixtureKind === 'step2-ready') return 0.95;
  // Real mode: edge-detection via OpenCV.js or simple Sobel kernel
  return 1.0; // optimistic default
}
```

### `prefers-reduced-motion` contract

- Light-meter pulse animation gated:
  ```css
  @media (prefers-reduced-motion: reduce) {
    .pu-lm-fill { transition: none; }
    .pu-shimmer, .pu-bgg-spinner, .pu-page-spin { animation: none; }
    .pu-cam-shutter:hover .pu-cam-shutter-inner { transform: none; }
  }
  ```
- Fallback: static gradient bar (no pulse) — light-meter still readable, just non-animated.
- Enforced via E2E spec `e2e/a11y/gamebook-upload.spec.ts` w/ `await page.emulateMedia({reducedMotion: 'reduce'})`.

---

## §10. Offline resilience contract

### Retry budget [1s, 2s, 4s, 8s, 16s] = 31s total

```ts
// lib/gamebook-upload/offline-budget.ts
export const RETRY_DELAYS_MS = [1_000, 2_000, 4_000, 8_000, 16_000] as const;
export const RETRY_BUDGET_TOTAL_MS = 31_000;

export interface OfflineBudgetState {
  attemptCount: number;          // 0..5
  nextRetryInMs: number | null;  // ms remaining; null when idle/exhausted
  isExhausted: boolean;
  abortController: AbortController | null; // null when idle
}

export type OfflineBudgetAction =
  | { type: 'NETWORK_ERROR' }
  | { type: 'RETRY_TICK' }      // dispatched every 100ms by host hook
  | { type: 'RETRY_FIRE' }      // dispatched when nextRetryInMs reaches 0
  | { type: 'RETRY_SUCCESS' }   // upload/poll succeeded → reset
  | { type: 'CANCEL' };         // user cancels → abort + reset

export function offlineBudgetReducer(state: OfflineBudgetState, action: OfflineBudgetAction): OfflineBudgetState {
  // ... pure reducer ...
}
```

### Idempotency-key strategy (MANDATORY)

Every photo upload request carries `Idempotency-Key: ${batchId}:${pageNumber}:${attemptCount}` header per AC-4.

**Server-side**: existing pattern Wave 3 spec-panel #732 — server dedup based on full key match. Same key + same body = idempotent retry; same key + different body = 409 Conflict (programmer error).

**Client-side composition**:

```ts
// lib/gamebook-upload/api-extension.ts
import { composeIdempotencyKey } from './schemas';

export async function uploadPhotoBatchIdempotent(
  batchId: string,
  gameId: string,
  photos: PhotoUploadItem[],
  attemptCount: number,
  signal: AbortSignal
): Promise<UploadPhotoBatchResponse> {
  const idempotencyKey = composeIdempotencyKey(batchId, photos[0].pageNumber, attemptCount);
  return apiClient.post(
    `/api/v1/gamebook/${gameId}/photos`,
    { gameId, photos },
    UploadPhotoBatchResponseSchema,
    { headers: { 'Idempotency-Key': idempotencyKey }, signal }
  );
}
```

The existing `usePhotoBatchUpload` mutation is extended (Interactions sub-PR) to thread `idempotencyKey` + `attemptCount` through `mutationFn`. Foundation skeleton uses fixture-only path; idempotency-key never sent in fixture mode.

### Cancel-during-retry (MANDATORY)

Orchestrator exposes `cancel()` callback that:

1. **Aborts in-flight request** via `AbortController.abort()` — browser cancels HTTP if not already settled
2. **Clears retry timer** — clears any pending `setTimeout` for next retry attempt
3. **Transitions FSM** to `wizard-cancelled` (via cancel-modal confirm)

**Cancel button visibility**: visible during all 5 retry attempts on `OfflineBanner` component. Tapping cancel button → opens `CancelModal` (NOT direct cancel) — destructive action requires confirmation.

```ts
const cancel = useCallback(() => {
  // 1. Abort in-flight HTTP
  abortControllerRef.current?.abort();
  abortControllerRef.current = null;

  // 2. Clear retry timer
  if (retryTimerRef.current) {
    clearTimeout(retryTimerRef.current);
    retryTimerRef.current = null;
  }

  // 3. Dispatch FSM transition
  dispatch({ type: 'CANCEL' });
}, []);
```

### In-flight HTTP on offline

Pending requests are NOT pre-emptively aborted on `navigator.onLine === false`. Browser handles network failure naturally — fetch rejects with `TypeError: Failed to fetch` (Chrome) or similar. Retry timer kicks in only AFTER `error` response settles.

User sees retry-timer-overlay countdown:
- "Tentativo 1/5 in 1s..."
- "Tentativo 2/5 in 2s..."
- ...
- "Tentativo 5/5 in 16s..."
- After 31s sum exhausted → `step3-failed-terminal` (manual recovery)

### 31s total budget visibility

`OfflineBanner` includes linear progress bar showing total elapsed (sum of completed delays + current countdown). Visual:

```
[█████████░░░░░░░░░░░] Tentativo 3/5 in 4s...
       elapsed=7s of 31s
```

`role="progressbar"` + `aria-valuenow={elapsedMs}` + `aria-valuemax={31000}` for SR accessibility.

### Failed terminal state

When budget exhausted (after 5 retries OR 31s sum), `step3-offline` transitions to `step3-failed-terminal`. UI shows:
- Banner: "Indicizzazione interrotta — riprova manualmente"
- Buttons: "Riprova" (manual retry, resets attempt count) + "Cambia foto" (re-enters step 2 with same batchId)
- Preserves uploaded thumbnails from earlier successful pages — user does NOT re-shoot pages that already uploaded
- Toast on partial success: "5 di 12 pagine caricate prima del problema"

### Degraded-polling fallback

If polling (`usePhotoBatchStatus`) detects ≥3 consecutive failures, transitions to "degraded mode":
- Polling interval increases from 2s → 10s
- UI banner: "Connessione lenta — controllo ogni 10s"
- Recovers on first successful poll back to 2s cadence

This is distinct from full offline (`navigator.onLine === false`). Degraded = slow network. Offline = no network.

---

## §11. Theme support

| Step | Theme | Background |
|------|-------|------------|
| Step 1 | Light default (matches MeepleAI app default) | `var(--bg)` |
| Step 2 (camera) | **Dark** (forced black `#000` for viewfinder) | Override via `data-theme="dark"` on `.pu-camera` root |
| Step 2 (denied) | Light default | `var(--bg)` |
| Step 2 (desktop drop) | Light default | `var(--bg)` |
| Step 3 | Light default | `var(--bg)` |
| CancelModal | Inherits parent theme | `var(--bg-card)` |

**Rationale**: Camera UI uses #000 background per UX convention (matches native iOS/Android camera apps + reduces glare in low-light environments). All other states inherit user's chosen theme.

**Dark mode global support**: full dark mode for non-camera surfaces is **deferred** to follow-up issue (NOT in SP6 scope, mirrors Wave B.2/B.3 light-only constraint).

---

## §12. Confidence-based retake logic

Confidence levels derived from per-page metadata (when backend exposes per-page) OR heuristic from batch-level avg + ordering (interim).

### Thresholds

| Level | Range | UX |
|-------|-------|----|
| `high` | ≥0.8 | Auto-accept, ✓ green badge, no action prompt |
| `medium` | 0.5..0.8 | Manual review allowed, ◐ amber badge, retake CTA hidden by default |
| `low` | <0.5 | Auto-flag retake, ⚠ red badge, retake CTA **visible** + page thumb red border |

### Classifier

```ts
// lib/gamebook-upload/confidence-classifier.ts
export function classifyConfidence(score: number | null): ConfidenceLevel | null {
  if (score === null) return null; // still processing
  if (score >= 0.8) return 'high';
  if (score >= 0.5) return 'medium';
  return 'low';
}

export function shouldRequestRetake(score: number | null): boolean {
  return classifyConfidence(score) === 'low';
}
```

### Heuristic (interim until per-page backend)

Backend currently exposes `averageConfidence` (batch-level 0..1). Until per-page tracking lands:

```ts
// Distribute batch-avg deterministically: assume uniform within batch unless fail count > 0
function deriveHeuristicPageConf(
  pageIndex: number,
  totalPages: number,
  batchAvg: number,
  failCount: number,
): number {
  if (failCount === 0) return batchAvg; // all uniform
  // Mark first N pages as 'low' where N = failCount
  if (pageIndex < failCount) return 0.4; // low conf
  return Math.min(1, batchAvg + 0.1);    // boost others
}
```

**Caveat documented**: this heuristic is approximate. When backend exposes per-page conf (Phase 3 Task 3.5b deferred), classifier uses real data. Documented in `lib/gamebook-upload/confidence-classifier.ts` comment.

---

## §13. 4 audit gates

### Gate A — ICU plural symmetry IT/EN (AC-7)

**What**: All ICU plural keys (`gamebook.upload.*.pagesCount`, `cancelModal.body`, etc.) must be symmetric across both locales — same plural categories.

**Where**: Foundation Task 1 + Interactions Task 1 add keys; checked via `pnpm i18n:check` script (or manual diff).

**Pass**: `it.json` and `en.json` have identical plural categories per ICU spec.

**Fail action**: refactor key to use both `one` + `other` even if one is unused (avoid `{count, plural, =1 {} other {}}` pattern).

### Gate B — Schema reality (Wave D.3 lesson)

**What**: Zod schemas must match real backend response shape — no schema reality drift.

**Where**: Foundation Task 1 — verify `UploadPhotoBatchResponseSchema` + `PhotoBatchStatusSchema` match actual API responses. New `IndexedPageMetaSchema` documents heuristic limitation per §12 — backend per-page conf deferred.

**Pass**: typecheck clean + sample response (`curl` or fixture) parses without error.

**Fail action**: file backend issue + adapt schema to current reality (with comment), defer full schema match.

### Gate C — MeepleCard fit evaluation

**What**: Decide whether `GameCard` component reuses MeepleCard primitive or diverges.

**Decision**: **DIVERGE** (per §5.3 rationale) — bespoke component due to:
- 4:3 aspect ratio cover with linear-gradient + emoji centered (no MeepleCard image slot fit)
- Selection ring (`box-shadow: 0 0 0 4px <orange>/.15`) not exposed by MeepleCard variants
- `sharedByCount` stat slot absent in MeepleCard

**Pass**: divergence documented in component file header + this contract §5.3 + matrix Wave D.1 pattern (PR #736).

**Fail action**: N/A — divergence is an explicit decision, not a failure.

### Gate D — Bootstrap-then-merge (PR #762 pattern)

**What**: visual baselines must be bootstrapped via `visual-regression-migrated.yml` workflow PRIOR to merge — never merge with `--admin` to skip missing-baselines failure.

**Where**: Foundation Task 5 + Interactions Task 5.

**Pass**: workflow run downloaded artifact, PNG committed, CI green.

**Fail action**: re-trigger workflow if a flaky failure; if persistent → debug fixture before merge. Never `--admin` past missing baselines (Wave A.1-A.5b lesson).

---

## §14. Visual baselines matrix

**Total: ~14 PNG** (11 states × desktop + 3 desktop+mobile pairs for representative states).

### Foundation sub-PR baselines (~7 PNG)

| Spec file | Snapshot | Viewport | Notes |
|-----------|----------|----------|-------|
| `e2e/visual-migrated/sp6-gamebook-upload-step1.spec.ts` | step1-default | 375 mobile | Catalog grid baseline |
| `e2e/visual-migrated/sp6-gamebook-upload-step1.spec.ts` | step1-default | 1440 desktop | Centered max-w-720 layout |
| `e2e/v2-states/gamebook-upload-step1.spec.ts` | step1-searching | 375 mobile | |
| `e2e/v2-states/gamebook-upload-step1.spec.ts` | step1-no-results | 375 mobile | 3 ActionCards |
| `e2e/v2-states/gamebook-upload-step1.spec.ts` | step1-bgg-loading | 375 mobile | Spinner + skeleton |
| `e2e/v2-states/gamebook-upload-step2-static.spec.ts` | step2-denied | 375 mobile | Permission denied (no real camera in Foundation) |
| `e2e/v2-states/gamebook-upload-step2-static.spec.ts` | step2-desktop-drop | 1440 desktop | DesktopDropFallback |

**N.B.**: Foundation sub-PR includes only **static** step 2/3 fixture renders (no camera, no upload). Real camera/upload baselines deferred to Interactions.

### Interactions sub-PR baselines (~7 PNG)

| Spec file | Snapshot | Viewport | Notes |
|-----------|----------|----------|-------|
| `e2e/v2-states/gamebook-upload-step2-camera.spec.ts` | step2-ready | 375 mobile | Camera viewfinder w/ light-meter ok (fixture-driven) |
| `e2e/v2-states/gamebook-upload-step2-camera.spec.ts` | step2-low-light | 375 mobile | Low-light state |
| `e2e/v2-states/gamebook-upload-step2-camera.spec.ts` | step2-failed | 375 mobile | Detection-failed state |
| `e2e/v2-states/gamebook-upload-step3.spec.ts` | step3-progress | 375 mobile | 24/50 progress + thumb grid |
| `e2e/v2-states/gamebook-upload-step3.spec.ts` | step3-partial | 375 mobile | 3 retake CTAs |
| `e2e/v2-states/gamebook-upload-step3.spec.ts` | step3-complete | 375 mobile | "Manuale pronto!" + 2-button footer |
| `e2e/v2-states/gamebook-upload-step3.spec.ts` | step3-offline | 375 mobile | Banner + retry countdown |

### Excluded from visual coverage (unit-tested only)

- `step2-capturing` (animation transient ≤500ms — flaky in CI)
- `step3-cancel-modal` (modal overlay — covered via component unit test + a11y E2E focus trap)

---

## §15. Test matrix

### Tier L 50/35/15 split

| Test type | Count | Coverage |
|-----------|-------|----------|
| Unit (Vitest) | ~165 | FSM cells + transitions, schemas (Zod parse), visual fixture, components (rendering + props), helpers (camera/offline/confidence) |
| Integration (MSW + TanStack) | ~25 | Mutation lifecycle (success/4xx/5xx/network/abort), polling cadence, idempotency-key header, cancel-during-retry abort propagation |
| E2E (Playwright) | ~22 | Visual-migrated (mockup parity) + v2-states (FSM cells) + a11y (axe-core WCAG AA) + smoke (real backend mocked at API level) + camera permission denied + offline retry + cancel modal focus trap |
| **Total** | **~212** | |

### Foundation breakdown

- **Unit**: ~30 lib + ~80 components (5 components × ~16 tests/component avg) = ~110
- **Integration**: 0 (no real hooks wired)
- **E2E**: ~10-12 (visual + v2-states + a11y for steps 1-3 static)

### Interactions breakdown

- **Unit**: ~40 lib (camera + offline + confidence) + ~80 components (6 components × ~13 tests) + ~35 orchestrator extension = ~155 (additive — Foundation Lockfile already counts)
  - Net new: ~55 unit (after excluding overlap with Foundation orchestrator skeleton tests)
- **Integration**: ~25 (MSW + TanStack — `lib/gamebook-upload/__integration__/`)
- **E2E**: ~10 (smoke + permission-denied + offline-retry + cancel-modal)

### Test file structure

```
apps/web/src/
├── lib/gamebook-upload/
│   ├── __tests__/
│   │   ├── fsm.test.ts                    # ~30 tests
│   │   ├── schemas.test.ts                # ~12 tests
│   │   ├── visual-test-fixture.test.ts    # ~20 tests
│   │   ├── camera-capabilities.test.ts    # ~15 tests
│   │   ├── offline-budget.test.ts         # ~18 tests
│   │   └── confidence-classifier.test.ts  # ~10 tests
│   └── __integration__/
│       ├── upload-mutation-lifecycle.test.ts   # ~10 tests (MSW)
│       ├── status-polling.test.ts              # ~8 tests
│       └── cancel-during-retry.test.ts         # ~7 tests
├── components/v2/gamebook-upload/
│   ├── __tests__/
│   │   ├── StepIndicator.test.tsx         # ~8
│   │   ├── GameSearchBar.test.tsx         # ~14
│   │   ├── GameCard.test.tsx              # ~12
│   │   ├── NoResultsPanel.test.tsx        # ~10
│   │   ├── ActionCard.test.tsx            # ~8
│   │   ├── CameraViewfinder.test.tsx      # ~22
│   │   ├── PageThumb.test.tsx             # ~14
│   │   ├── ConfidenceBadge.test.tsx       # ~8
│   │   ├── OfflineBanner.test.tsx         # ~14
│   │   ├── CancelModal.test.tsx           # ~12
│   │   └── DesktopDropFallback.test.tsx   # ~10
└── app/(authenticated)/gamebook/upload/_components/
    ├── __tests__/
    │   └── GamebookUploadView.test.tsx    # ~40 (covers all 11 FSM cells)
e2e/
├── visual-migrated/
│   ├── sp6-gamebook-upload-step1.spec.ts
│   ├── sp6-gamebook-upload-step2.spec.ts (Interactions)
│   └── sp6-gamebook-upload-step3.spec.ts (Interactions)
├── v2-states/
│   ├── gamebook-upload-step1.spec.ts
│   ├── gamebook-upload-step2-static.spec.ts (Foundation: denied + desktop-drop)
│   ├── gamebook-upload-step2-camera.spec.ts (Interactions)
│   └── gamebook-upload-step3.spec.ts (Interactions)
├── a11y/
│   └── gamebook-upload.spec.ts (axe-core + reduced-motion + focus-trap)
└── smoke/
    └── gamebook-upload-real-backend.spec.ts (Interactions, MSW-mocked at API)
```

---

## §16. Foundation+Interactions sub-PR breakdown

### Foundation sub-PR — 5 tasks SERIAL

**Branch**: `feature/issue-789-upload-foundation` from `main-dev`

#### Task C.1.A — Foundation lib + schemas + i18n
- Files: `lib/gamebook-upload/{fsm.ts,schemas.ts,visual-test-fixture.ts,index.ts}` + tests
- Files: `locales/{it,en}.json` (~50 keys under `gamebook.upload.*`)
- Tests: ~30 unit (FSM 11 states + transitions + parsers + ICU plural symmetry)
- Commit: `feat(gamebook-upload): SP6 Phase C Foundation Task A — lib + schemas + i18n (#789)`

#### Task C.1.B — 5 read-only components
- Files: `components/v2/gamebook-upload/{StepIndicator,GameSearchBar,GameCard,NoResultsPanel,ActionCard}.tsx` + tests + index barrel
- Tests: ~52 unit (8+14+12+10+8 per component)
- Commit: `feat(gamebook-upload): SP6 Phase C Foundation Task B — 5 read-only components (#789)`

#### Task C.1.C — Orchestrator skeleton + page wiring (static fixture mode)
- Files: `app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` (orchestrator skeleton) + `page.tsx` (Suspense shell — REPLACES existing `GamebookUploadClient`)
- Tests: ~28 unit (URL state SSOT + 11 FSM cells × `?fixture=` override matrix)
- **Big-Bang delete legacy**: Foundation removes `_components/GamebookUploadClient.tsx`, `_components/PhotoUploader.tsx`, `_components/ConfidenceBadge.tsx` (Wave B.3 single-tree responsive uniformity pattern)
- Commit: `feat(gamebook-upload): SP6 Phase C Foundation Task C — orchestrator skeleton + Big-Bang legacy delete (#789)`

#### Task C.1.D — E2E specs (static fixtures)
- Files: `e2e/visual-migrated/sp6-gamebook-upload-step1.spec.ts`, `e2e/v2-states/gamebook-upload-step1.spec.ts`, `e2e/v2-states/gamebook-upload-step2-static.spec.ts`, `e2e/a11y/gamebook-upload.spec.ts` (foundation-scope: steps 1 + step2-denied + step2-desktop-drop)
- Tests: ~12 specs total
- Commit: `test(gamebook-upload): SP6 Phase C Foundation Task D — E2E specs static fixtures (#789)`

#### Task C.1.E — Bootstrap baselines + matrix update + PR
- Trigger `visual-regression-migrated.yml` workflow (mode=bootstrap)
- Download PNG artifact (~7 PNG) → commit to spec snapshot dirs
- Update `docs/for-developers/frontend/v2-migration-matrix.md` — add `/gamebook/upload` row Tier L Foundation status `done`, PR `#<FOUNDATION_PR>`
- Open Foundation PR base `main-dev`, body lists all 5 tasks + Gates A/B/C/D status
- Merge `--squash --delete-branch` after CI green
- Commit: `test(gamebook-upload): SP6 Phase C Foundation Task E — baselines bootstrap + matrix update (#789)`

**Expected after Foundation**: ~110 unit tests + 12 E2E specs + ~7 PNG baselines + 5 commits + 1 merged PR.

### Interactions sub-PR — 5 tasks SERIAL

**Pre-condition**: Foundation PR merged. New branch from updated `main-dev`.

**Branch**: `feature/issue-789-upload-interactions` from `main-dev`

#### Task C.2.A — Camera + offline + confidence libs
- Files: `lib/gamebook-upload/{camera-capabilities,offline-budget,confidence-classifier,api-extension}.ts` + tests
- Tests: ~43 unit (15 camera + 18 offline + 10 confidence)
- Commit: `feat(gamebook-upload): SP6 Phase C Interactions Task A — camera + offline + confidence libs (#789)`

#### Task C.2.B — 6 interactive components
- Files: `components/v2/gamebook-upload/{CameraViewfinder,PageThumb,ConfidenceBadge,OfflineBanner,CancelModal,DesktopDropFallback}.tsx` + tests
- `CancelModal` lazy-loaded via `React.lazy()` per bundle budget
- Tests: ~80 unit (22+14+8+14+12+10)
- Commit: `feat(gamebook-upload): SP6 Phase C Interactions Task B — 6 interactive components (#789)`

#### Task C.2.C — Orchestrator extension + side-effect wiring
- Files: extend `GamebookUploadView.tsx` with usePhotoBatchUpload + usePhotoBatchStatus + camera capability + offline budget hooks; FSM `fsm.ts` UNCHANGED (Foundation already complete)
- Tests: ~35 unit (orchestrator state composition)
- Commit: `feat(gamebook-upload): SP6 Phase C Interactions Task C — orchestrator extension (#789)`

#### Task C.2.D — Integration tests (Crispin missing tier)
- Files: `lib/gamebook-upload/__integration__/{upload-mutation-lifecycle,status-polling,cancel-during-retry}.test.ts`
- Uses MSW + TanStack Query test setup
- Tests: ~25 integration
- Commit: `test(gamebook-upload): SP6 Phase C Interactions Task D — integration tests (#789)`

#### Task C.2.E — E2E + baselines + matrix update + PR (+ bundle rebaseline if needed)
- Files: `e2e/visual-migrated/sp6-gamebook-upload-step{2,3}.spec.ts`, `e2e/v2-states/gamebook-upload-step2-camera.spec.ts`, `e2e/v2-states/gamebook-upload-step3.spec.ts`, `e2e/smoke/gamebook-upload-real-backend.spec.ts`, extend `e2e/a11y/gamebook-upload.spec.ts` (camera + cancel-modal focus trap)
- Trigger workflow → download PNG (~7 PNG) → commit
- Update matrix to mark Interactions `done`
- Open Interactions PR base `main-dev`
- Bundle delta check: if exceeds budget, separate `chore(bundle-size): rebaseline post SP6 Phase C` PR per Wave D.2 #754 pattern
- Commit: `test(gamebook-upload): SP6 Phase C Interactions Task E — E2E + baselines + matrix (#789)`

**Expected after Interactions**: ~55 net new unit + 25 integration + 10 E2E specs + ~7 PNG baselines + 5 commits + 1 merged PR + optional bundle rebaseline PR.

---

## §17. Acceptance criteria (12 ACs hoisted from plan + Phase C-specific)

1. **AC-1** (plan): 3-step wizard `/gamebook/upload` URL-navigable via `?step=1|2|3` SSOT (deep-link to step 2 with `?gameId=` skips step 1; step 3 requires `?batchId=`).
2. **AC-2** (plan): Offline retry budget exhausts to `failed` state after 31s total ([1+2+4+8+16]=31s) with cancel button visible during all 5 retry attempts.
3. **AC-3** (plan): Camera permission denied → fallback to file picker within ≤500ms inline UI swap (NO modal interruption).
4. **AC-4** (plan): All photo upload requests carry `Idempotency-Key: ${batchId}:${pageNumber}:${attemptCount}` header.
5. **AC-5** (plan): Bundle delta ≤+120 KB Phase C (Foundation ~50 + Interactions ~70). Total SP6 umbrella ≤+170 KB.
6. **AC-6** (plan): All visual baselines bootstrapped via Gate D bootstrap-then-merge (PR #762 pattern) — no `--admin` merge for missing baselines.
7. **AC-7** (plan): ICU plural keys symmetric IT/EN for `gamebook.upload.*.pagesCount` + `cancelModal.body` + `step3.partial.subtitle` etc.
8. **AC-8**: 11-state FSM rendered correctly per visual fixture overrides — all cells covered by Vitest unit tests + Playwright v2-states snapshots.
9. **AC-9**: `CancelModal` focus trap operates per Wave D.2 ESC behavior matrix (ESC = cancel-cancel; outside click = cancel-cancel; confirm = wizard-cancelled). Initial focus on Continue button (safe default).
10. **AC-10**: Camera 4-permission-state matrix (`granted`/`denied`/`prompt`/`unsupported`) tested via E2E with `--use-fake-device-for-media-stream` (Chromium) + `MediaStream` global mock (Webkit/Firefox).
11. **AC-11**: `prefers-reduced-motion: reduce` disables light-meter pulse animation, shutter inner-scale, page-spin, BGG spinner, shimmer — verified via `await page.emulateMedia({reducedMotion: 'reduce'})` in a11y spec.
12. **AC-12**: WCAG AA color contrast ≥4.5:1 for all text + ≥3:1 for interactive UI per axe-core checks; reduced-motion safe; semantic HTML; ARIA labels for all interactive elements.

---

## §18. Open questions (deferred to implementation)

1. **BGG search API exposure**: `useSearchBggGames` hook exists. Audit during Foundation Task A whether endpoint returns `BggSearchResultSchema`-compatible shape OR needs adapter. If adapter needed, document in Foundation lib README.
2. **Per-page confidence backend**: backend currently exposes only `averageConfidence`. Per-page tracking deferred to Phase 3 Task 3.5b. Heuristic per §12 documents interim logic.
3. **`useCameraCapabilities` SSR-safe boundary**: orchestrator must not call `navigator.mediaDevices` during SSR. Wrap in `'use client'` + `useEffect` mount-only init.
4. **DesktopDropFallback HEIC support**: backend accepts HEIC per existing schema. Browser HEIC preview support varies — may need conversion via `heic-to` lib (~+20 KB). Decide in Interactions Task B; fallback shows generic file icon if browser cannot render.
5. **Light-meter heuristic accuracy**: real video stream luminance sampling via 16×16 grid may be jittery. Consider 200ms debounce + EMA smoothing. Tune in Interactions Task A; defer to follow-up if too noisy.
6. **DocumentProcessing BC ownership for upload + indexing endpoints**: confirm with backend audit Phase B.0 — fixture mode in Foundation tolerates ambiguity.

---

## §19. Coexistence flag

**Big-Bang replacement of existing partial impl** (Wave B.3 pattern):

- Foundation Task C deletes `_components/{GamebookUploadClient,PhotoUploader,ConfidenceBadge}.tsx` + `_components/__tests__/*` files
- New orchestrator `GamebookUploadView.tsx` REPLACES `GamebookUploadClient.tsx` as default export from `_components/`
- `page.tsx` Suspense shell updated to render `<GamebookUploadView />` directly
- No coexistence flag needed — single-tree v2 build from start of Foundation

**Rationale**: existing impl is partial (single-page upload, no wizard). Replacing wholesale is cleaner than coexistence per Wave B.3 single-tree responsive uniformity lesson. URL `/gamebook/upload?gameId=` deep-link still works (Foundation orchestrator parses `?gameId` → starts at step 2).

---

## §20. References

### Pattern parents
- **Wave D.3 PR #762** (`/sessions/[id]/summary` Tier M-L 5-task TDD blueprint) — `docs/for-developers/frontend/contracts/sessions-id-summary-hooks.md` (1199 lines)
- **Wave D.2 PR #749 + #753** (`/sessions/[id]/live` Tier L+ Foundation+Interactions blueprint) — `docs/for-developers/frontend/contracts/sessions-id-live-hooks.md` (826 lines)
- **Wave D.2 PR #754** (bundle rebaseline pattern)
- **Wave C.1 PR #702** (Phase 0.5 sub-hook contract pattern, 4-hook composition)
- **Wave 4 D1 PR #717** (Tier S single-shot blueprint, no Phase 0.5)
- **Wave B.3 PR #638** (single-tree responsive uniformity, Big-Bang legacy delete)
- **Wave B.1 PR #635** (audit-driven scope reduction for pre-existing token debt)

### Memory pointers
- `session_2026-05-06_wave-d3-shipped.md` — most recent pattern reference
- `session_2026-05-06_wave-d2-end-to-end.md` — Tier L+ Foundation+Interactions retrospective
- `session_2026-05-05_wave-d-spec-panel.md` — Wave D 5-expert review
- `feedback_subagent-serial-only.md` — never parallel dispatch
- `feedback_brownfield-route-redirect-audit.md` — pre-impl redirect cleanup

### Spec-panel review amendments (plan §Spec-panel review amendments items 5-9)
- **Item 5** (Nygard + Crispin): Camera contract §9 — 4-permission-state matrix + Playwright `--use-fake-device-for-media-stream` + light-meter deterministic input + reduced-motion
- **Item 6** (Nygard): Cancel-during-retry + idempotency-key §10
- **Item 7** (Fowler): FSM lives entirely in Foundation `fsm.ts` — declared §3 ownership boundary
- **Item 8** (Crispin): Integration tier tests Task C.2.D — ~25 MSW + TanStack tests
- **Item 9** (Cockburn): Phase D OUT OF SCOPE rationale in plan §Scope (referenced — `/gamebook/[gameId]/play` paragraph navigation v2 redesign deferred)

### Mockup sources (visual parity targets)
- `admin-mockups/design_files/sp6-libro-game-photo-upload.html` — full HTML mockup
- `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx` — React JSX with 14 STATES catalog (line 729-747)

### Backend hooks reused as-is (no schema changes)
- `apps/web/src/lib/gamebook/hooks/usePhotoBatchUpload.ts` (mutation)
- `apps/web/src/lib/gamebook/hooks/usePhotoBatchStatus.ts` (polling)
- `apps/web/src/hooks/queries/useGames.ts` (catalog)
- `apps/web/src/hooks/queries/useSearchBggGames.ts` (BGG search)

### Existing schemas reused
- `apps/web/src/lib/gamebook/schemas.ts` — `PhotoBatchStatusSchema`, `UploadPhotoBatchResponseSchema`, `BATCH_TERMINAL_STATUSES`, `isBatchTerminal()`, `batchProgressPercent()`

### Followup issues / deferred work
- Real translation endpoint exposure (Phase 3 Task 3.5e — separate plan)
- Per-page confidence backend tracking (Phase 3 Task 3.5b)
- `/gamebook/[gameId]/play` paragraph navigation v2 redesign (separate plan post-SP6)
- Premium tier purchase flow (separate epic)
- Full dark mode support for non-camera surfaces (follow-up issue)
- `heic-to` library evaluation if HEIC preview required on desktop drop (Interactions decision)

---

**Status**: ✅ Phase 0.5 contract DRAFT complete. Ready for Foundation+Interactions dispatch per plan §Phase C Tasks C.1 + C.2.

**Next steps**:
1. Commit + push contract PR
2. Wait CI green (docs-only, fast)
3. Merge `--squash --delete-branch`
4. Dispatch Foundation 5-task SERIAL per §16
5. Wait Foundation PR merged
6. Dispatch Interactions 5-task SERIAL per §16
7. Optional: bundle rebaseline PR if budget exceeded
