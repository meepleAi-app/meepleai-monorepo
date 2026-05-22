/**
 * Schema definitions for `/gamebook/upload` 3-step wizard (SP6 Phase C.1.A).
 *
 * Schema reality v1 carryover (Gate B):
 *   - `BggSearchResultSchema` shape is aspirational — pending BGG search
 *     endpoint audit. Real `useSearchBggGames` hook (declared by contract §3
 *     row 4) has not been verified against backend yet. The Phase C.1.B
 *     Interactions sub-PR is responsible for adapting whatever the backend
 *     actually returns. Until then, fixtures use the contract shape directly.
 *
 *   - `IndexedPageMetaSchema` documents the per-page confidence limitation —
 *     backend currently exposes only `averageConfidence` at batch level. The
 *     `confidence` field is set via the heuristic in `confidence-classifier.ts`
 *     (Phase C.1.B) until backend tracks per-page (deferred per contract §12).
 *
 * Backend reuse: the existing `lib/gamebook/schemas.ts` exposes
 * `PhotoBatchStatusSchema`, `BATCH_TERMINAL_STATUSES`, `isBatchTerminal()`,
 * `batchProgressPercent()`, `UploadPhotoBatchRequestSchema` and friends. They
 * stay there to keep gamebook (consumption) decoupled from gamebook-upload
 * (production). This module adds NEW schemas for wizard-specific concerns:
 *   - Step 1: catalog/BGG search results, no-results action discriminant
 *   - Step 2: camera permission state, light-meter reading, captured page
 *   - Step 3: confidence level + per-page metadata
 *   - Cross-cutting: idempotency-key composer + retry budget state
 *
 * Used by:
 *   - `apps/web/src/lib/gamebook-upload/fsm.ts`
 *   - `apps/web/src/lib/gamebook-upload/visual-test-fixture.ts`
 *   - `apps/web/src/lib/gamebook-upload/idempotency-key.ts`
 *   - `apps/web/src/components/features/gamebook-upload/*` (Phase C.1.B Task 2)
 *   - `apps/web/src/app/(authenticated)/gamebook/upload/*` (Phase C.1.B Task 3)
 */

import { z } from 'zod';

// ============================================================================
// Step 1 — Game selection
// ============================================================================

/**
 * Discriminant for the active search tab (catalog vs BGG remote).
 */
export const GameSearchTabSchema = z.enum(['catalog', 'bgg']);
export type GameSearchTab = z.infer<typeof GameSearchTabSchema>;

/**
 * Catalog game reference returned by the existing `useGames` hook (verified
 * shape per contract §3). Backend exposes title + publisher + cover, plus
 * `sharedByCount` for the community indicator chip and `isIndexed` flag.
 */
export const CatalogGameRefSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(1),
  publisher: z.string().nullable(),
  coverImageUrl: z.string().url().nullable(),
  sharedByCount: z.number().int().nonnegative(),
  isIndexed: z.boolean(),
});
export type CatalogGameRef = z.infer<typeof CatalogGameRefSchema>;

/**
 * BGG search result. Fields aspirational until BGG search endpoint audit
 * lands (Gate B v1 carryover). Phase C.1.B Interactions sub-PR will adapt
 * the real hook response to this canonical shape.
 */
export const BggSearchResultSchema = z.object({
  bggId: z.number().int().positive(),
  title: z.string().min(1),
  publisher: z.string().nullable(),
  yearPublished: z.number().int().nullable(),
});
export type BggSearchResult = z.infer<typeof BggSearchResultSchema>;

/**
 * Discriminant for the 3 ActionCards rendered when search yields 0 results.
 *   - `create-new`     opens the "create gamebook" modal (no game in catalog/BGG)
 *   - `search-bgg`     switches active tab to BGG and triggers remote search
 *   - `index-private`  generates a synthetic gameId, marks as private (not shared)
 */
export const NoResultsActionSchema = z.enum(['create-new', 'search-bgg', 'index-private']);
export type NoResultsAction = z.infer<typeof NoResultsActionSchema>;

// ============================================================================
// Step 2 — Camera capture
// ============================================================================

/**
 * 4-state camera permission matrix (per contract §9).
 *
 *   - `granted`     full UX with native viewfinder
 *   - `denied`      fallback to file picker via inline UI swap (≤500ms)
 *   - `prompt`      initial state, show "Allow camera access" CTA
 *   - `unsupported` `mediaDevices.getUserMedia` undefined → fallback only
 */
export const CameraPermissionStateSchema = z.enum(['granted', 'denied', 'prompt', 'unsupported']);
export type CameraPermissionState = z.infer<typeof CameraPermissionStateSchema>;

/**
 * Light-meter reading sampled from the live MediaStream (or fixture override
 * during E2E tests per contract §9). Value is normalized 0..1 and bucketed.
 */
export const LightMeterReadingSchema = z.object({
  value: z.number().min(0).max(1),
  level: z.enum(['too-dark', 'low', 'medium', 'ok']),
});
export type LightMeterReading = z.infer<typeof LightMeterReadingSchema>;

/**
 * Optimistic captured-page entry. `thumbObjectUrl` is a client-side
 * `URL.createObjectURL()` reference revoked on unmount. `pendingUpload`
 * is true between shutter tap and successful batch upload settlement.
 */
export const CapturedPageSchema = z.object({
  pageNumber: z.number().int().positive(),
  thumbObjectUrl: z.string(),
  pendingUpload: z.boolean(),
});
export type CapturedPage = z.infer<typeof CapturedPageSchema>;

// ============================================================================
// Step 3 — Indexing progress
// ============================================================================

/**
 * 3-bucket confidence classifier (per contract §12 thresholds).
 *
 *   - `high`   score ≥0.8  → ✓ green badge, auto-accept
 *   - `medium` 0.5..0.8    → ◐ amber badge, manual review allowed
 *   - `low`    score <0.5  → ⚠ red badge, retake CTA visible
 */
export const ConfidenceLevelSchema = z.enum(['high', 'medium', 'low']);
export type ConfidenceLevel = z.infer<typeof ConfidenceLevelSchema>;

/**
 * Per-page indexing metadata derived from batch status + heuristic.
 *
 * Schema reality v1 carryover (Gate B): backend currently exposes only
 * batch-level `averageConfidence`. Per-page conf is inferred from ordering +
 * fail-count heuristic in `confidence-classifier.ts` (Phase C.1.B) until
 * backend tracks per-page (deferred per contract §12).
 *
 *   - `confidence` is null while OCR is still running on this page
 *   - `isProcessing` true between batch start and per-page settlement
 *   - `retakeRequested` true when classifier returns 'low' AND user surfaces
 *     the retake prompt
 */
export const IndexedPageMetaSchema = z.object({
  pageNumber: z.number().int().positive(),
  confidence: ConfidenceLevelSchema.nullable(),
  isProcessing: z.boolean(),
  retakeRequested: z.boolean(),
});
export type IndexedPageMeta = z.infer<typeof IndexedPageMetaSchema>;

// ============================================================================
// Offline retry budget
// ============================================================================

/**
 * Exponential retry delays (per contract §10). Sum = 31_000ms.
 *
 * The 5 attempts model graceful network recovery — phone with intermittent
 * WiFi typically recovers within 31s. After the budget is exhausted the FSM
 * transitions to `step3-failed-terminal` with a manual-recovery UI.
 */
export const RETRY_DELAYS_MS = [1_000, 2_000, 4_000, 8_000, 16_000] as const;

/**
 * Sum of all retry delays = 31_000ms. Computed once at module load.
 */
export const RETRY_BUDGET_TOTAL_MS = RETRY_DELAYS_MS.reduce((a, b) => a + b, 0);

/**
 * Pure state shape for the offline-budget reducer (Phase C.1.B
 * Interactions sub-PR composes this into a hook).
 *
 *   - `attemptCount`   how many retries fired so far (0..5)
 *   - `nextRetryInMs`  ms remaining until next fire; null when idle/exhausted
 *   - `isExhausted`    true once attemptCount === 5 and timer settled
 */
export const RetryStateSchema = z.object({
  attemptCount: z.number().int().min(0).max(5),
  nextRetryInMs: z.number().int().nullable(),
  isExhausted: z.boolean(),
});
export type RetryState = z.infer<typeof RetryStateSchema>;

// ============================================================================
// Wizard fixture discriminant (visual-test override)
// ============================================================================

/**
 * Discriminant for the `?fixture=` URL hatch (gated by `STATE_OVERRIDE_ENABLED`
 * in `visual-test-fixture.ts`). Exposes 14 cell variants matching the FSM:
 *
 *   Step 1: step1-default | step1-searching | step1-no-results | step1-bgg-loading
 *   Step 2: step2-ready | step2-capturing | step2-low-light | step2-failed | step2-denied
 *   Step 3: step3-progress | step3-partial | step3-complete | step3-offline | step3-cancel-modal
 *
 * The `wizard-cancelled` terminal cell is NOT a fixture kind because
 * orchestrator routes the user to `/gamebook` upon entering it — there is
 * nothing to render at `/gamebook/upload?fixture=wizard-cancelled`.
 */
export const wizardFixtureKindSchema = z.enum([
  'step1-default',
  'step1-searching',
  'step1-no-results',
  'step1-bgg-loading',
  'step2-ready',
  'step2-capturing',
  'step2-low-light',
  'step2-failed',
  'step2-denied',
  'step3-progress',
  'step3-partial',
  'step3-complete',
  'step3-offline',
  'step3-cancel-modal',
]);
export type WizardFixtureKind = z.infer<typeof wizardFixtureKindSchema>;
