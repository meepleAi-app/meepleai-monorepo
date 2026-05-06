/**
 * 11-state FSM derivation for `/gamebook/upload` 3-step wizard
 * (SP6 Phase C.1.A Foundation). Pure function — no React, no API client.
 *
 * Foundation owns the ENTIRE FSM per contract §3 + plan §Phase C ownership
 * boundary. Interactions sub-PR (Phase C.1.B) adds side-effect handlers ONLY;
 * it never mutates cell shape or transition logic. Per Wave D.2 PR #749 lesson
 * AP10 (cherry-pick chain anti-pattern).
 *
 * Cells are partitioned across 3 wizard steps + a cross-cutting modal/terminal:
 *
 *   Step 1 — Game selection (4 cells):
 *     step1-default       initial render, catalog grid + search bar idle
 *     step1-searching     search input typed (250ms debounce), results filtering
 *     step1-no-results    search query yields 0 catalog matches → 3 ActionCards
 *     step1-bgg-loading   BGG tab clicked OR ActionCard "Cerca su BGG" selected
 *
 *   Step 2 — Photo capture (5 cells):
 *     step2-ready         camera granted + light OK + page detected → shutter armed
 *     step2-capturing     mid-shot animation (≤500ms transient state)
 *     step2-low-light     light-meter <30% → frame yellow, shutter disabled
 *     step2-failed        page-detection corner heuristic fails → frame red dashed
 *     step2-denied        camera permission denied OR unsupported → file picker fallback
 *
 *   Step 3 — Indexing progress (4 cells + modal overlay):
 *     step3-progress      polling Pending/Processing → progress bar + thumb grid
 *     step3-partial       Completed AND ≥1 page conf<0.5 → retake prompts
 *     step3-complete      Completed AND all conf≥0.5 → success state
 *     step3-offline       network failure ≥5s OR navigator.onLine === false
 *     step3-cancel-modal  user tapped × close on step 2/3 with capturedCount > 0
 *
 *   Terminal (1 cell):
 *     wizard-cancelled    user confirmed cancel from modal OR back-arrow on step 1
 *
 * NOTE: `step3-failed-terminal` (manual recovery after retry budget exhausted)
 * is documented in contract §10 but lives in Phase C.1.B Interactions because
 * it depends on the offline-budget reducer state. Foundation FSM stops at
 * `step3-offline`; Interactions extends with the failed-terminal transition.
 *
 * Cell precedence (highest → lowest):
 *   1. cancelModalOpen      → step3-cancel-modal (overrides everything)
 *   2. step === 1           → step1-* derived from search state
 *   3. step === 2 + denied  → step2-denied
 *   4. step === 2           → step2-* derived from camera + light + detection
 *   5. step === 3 + offline → step3-offline (overrides progress/partial/complete)
 *   6. step === 3           → step3-* derived from batch status
 */

import type { BggSearchResult, CameraPermissionState, CatalogGameRef } from './schemas';

// ---------------------------------------------------------------------------
// Wizard step indicator
// ---------------------------------------------------------------------------

/**
 * Wizard step number, mirrors `?step=` URL param. SSOT for navigation.
 */
export type WizardStep = 1 | 2 | 3;

// ---------------------------------------------------------------------------
// Cell discriminated union
// ---------------------------------------------------------------------------

/**
 * 14 visible cells (11 numbered states + 1 modal overlay + 1 transient + 1 terminal)
 * exhaustively cover all wizard paths. Each cell carries the data the
 * orchestrator + components need to render — no shared "current state" object.
 */
export type WizardFSMCell =
  // Step 1: Game selection (4 cells)
  | {
      kind: 'step1-default';
      catalogResults: readonly CatalogGameRef[];
      selectedGameId: string | null;
    }
  | {
      kind: 'step1-searching';
      query: string;
      activeTab: 'catalog' | 'bgg';
      catalogResults: readonly CatalogGameRef[];
      bggResults: readonly BggSearchResult[];
    }
  | {
      kind: 'step1-no-results';
      query: string;
    }
  | {
      kind: 'step1-bgg-loading';
      query: string;
    }
  // Step 2: Photo capture (5 cells)
  | {
      kind: 'step2-ready';
      gameId: string;
      permissionState: 'granted' | 'prompt';
      capturedCount: number;
    }
  | {
      kind: 'step2-capturing';
      gameId: string;
      capturedCount: number;
    }
  | {
      kind: 'step2-low-light';
      gameId: string;
      capturedCount: number;
    }
  | {
      kind: 'step2-failed';
      gameId: string;
      capturedCount: number;
      detectionScore: number;
    }
  | {
      kind: 'step2-denied';
      gameId: string;
      permissionState: 'denied' | 'unsupported';
    }
  // Step 3: Indexing (4 cells)
  | {
      kind: 'step3-progress';
      batchId: string;
      processedPages: number;
      totalPages: number;
    }
  | {
      kind: 'step3-partial';
      batchId: string;
      processedPages: number;
      totalPages: number;
      lowConfidencePages: readonly number[];
    }
  | {
      kind: 'step3-complete';
      batchId: string;
      totalPages: number;
    }
  | {
      kind: 'step3-offline';
      batchId: string;
      retryAttempt: number;
      nextRetryInMs: number;
    }
  // Modal overlay — additive, NOT a standalone state but a cell because the
  // orchestrator must render the underlying step's UI behind it.
  | {
      kind: 'step3-cancel-modal';
      batchId: string;
      previousCell: WizardFSMCell;
    }
  // Terminal
  | {
      kind: 'wizard-cancelled';
      reason: 'user-action' | 'timeout';
    };

// ---------------------------------------------------------------------------
// Input shapes (mirror TanStack Query's UseQueryResult slice)
// ---------------------------------------------------------------------------

/**
 * Minimal slice of TanStack `UseQueryResult` we actually need. Independent
 * type so this module stays decoupled from `@tanstack/react-query`.
 */
export interface QueryLike<TData> {
  readonly isPending: boolean;
  readonly isError: boolean;
  readonly error?: Error | null;
  readonly data?: TData | null;
  readonly query?: string;
}

/**
 * Composite input shape for `deriveWizardState`. Orchestrator (Phase C.1.B)
 * assembles this from URL params + hook results before each render.
 */
export interface DeriveStateInput {
  /** Wizard step from URL `?step=`. Foundation parser clamps to 1|2|3. */
  readonly step: WizardStep;

  /** SSOT for step-1 → step-2 transition. Set on game selection. */
  readonly gameId: string | null;

  /** SSOT for step-2 → step-3 transition. Set on batch upload success. */
  readonly batchId: string | null;

  /** Catalog search query state. */
  readonly gameSearchQuery: QueryLike<readonly CatalogGameRef[]>;

  /** Raw search input (debounced 250ms). Empty string = idle. */
  readonly searchInput: string;

  /** Active tab (catalog vs bgg). Drives BGG-loading cell visibility. */
  readonly activeTab: 'catalog' | 'bgg';

  /** BGG remote search query state (only consumed on activeTab='bgg'). */
  readonly bggSearchQuery: QueryLike<readonly BggSearchResult[]>;

  /** Camera permission state. Drives step2-* cells. */
  readonly cameraPermission: CameraPermissionState;

  /** 0..1 normalized brightness from MediaStream sampling. */
  readonly lightMeterValue: number;

  /** Page-detection corner score 0..1. <0.5 = detection failed. */
  readonly detectionScore: number;

  /** Mid-shot animation flag. True for ≤500ms after shutter tap. */
  readonly isCapturing: boolean;

  /** Optimistic captured count (Phase C.1.B). */
  readonly capturedCount: number;

  /** Batch status query (polling Pending/Processing/Completed/Failed). */
  readonly batchStatus: QueryLike<{
    status: string;
    totalPages: number;
    processedPages: number;
    averageConfidence: number | null;
    failedPageNumbers?: readonly number[];
  }>;

  /** True when navigator.onLine === false OR fetch failed ≥5s. */
  readonly isOffline: boolean;

  /** 0..5 attempts spent. Drives offline retry messaging. */
  readonly retryAttempt: number;

  /** ms until next retry fire. 0 when idle. */
  readonly nextRetryInMs: number;

  /** True when user tapped × close with capturedCount > 0. */
  readonly cancelModalOpen: boolean;

  /** True when user confirmed the cancel-modal OR back-arrow on step 1. */
  readonly isWizardCancelled: boolean;
}

// ---------------------------------------------------------------------------
// Threshold constants
// ---------------------------------------------------------------------------

/**
 * Light-meter threshold below which the camera shows the low-light hint.
 * Matches contract §2 step2-low-light row (light-meter <30%).
 */
export const LIGHT_LOW_THRESHOLD = 0.3;

/**
 * Page-detection corner-heuristic threshold. <0.5 → step2-failed.
 * Matches contract §9 deterministic input map (`step2-failed` returns 0.2).
 */
export const PAGE_DETECTION_THRESHOLD = 0.5;

/**
 * Confidence threshold for retake — cells with conf<0.5 are flagged.
 * See `confidence-classifier.ts` (Phase C.1.B) for full classifier.
 */
export const LOW_CONFIDENCE_THRESHOLD = 0.5;

// ---------------------------------------------------------------------------
// Derivation
// ---------------------------------------------------------------------------

/**
 * Derive the FSM cell from URL state + hook results.
 *
 * Cell kinds are mutually exclusive — the function returns exactly one cell.
 * The discriminant `kind` is narrow-typeable in an exhaustive switch.
 *
 * Precedence is enforced top-down: cancel-modal overrides everything;
 * within a step, errored/offline states override progress/idle; transient
 * states (capturing, bgg-loading) override their settled counterparts.
 */
export function deriveWizardState(input: DeriveStateInput): WizardFSMCell {
  // Terminal — wizard cancelled (overrides everything except still-rendering modal)
  if (input.isWizardCancelled) {
    return { kind: 'wizard-cancelled', reason: 'user-action' };
  }

  // Modal overlay — must render with previous step's UI behind. Computed
  // recursively but with `cancelModalOpen=false` to avoid infinite loop.
  if (input.cancelModalOpen && input.batchId !== null) {
    const previousCell = deriveWizardState({ ...input, cancelModalOpen: false });
    return {
      kind: 'step3-cancel-modal',
      batchId: input.batchId,
      previousCell,
    };
  }

  // Step 1 — Game selection
  if (input.step === 1) {
    return deriveStep1Cell(input);
  }

  // Step 2 — Photo capture
  if (input.step === 2) {
    return deriveStep2Cell(input);
  }

  // Step 3 — Indexing
  return deriveStep3Cell(input);
}

// ---------------------------------------------------------------------------
// Per-step derivation helpers
// ---------------------------------------------------------------------------

function deriveStep1Cell(input: DeriveStateInput): WizardFSMCell {
  const trimmedQuery = input.searchInput.trim();

  // BGG tab loading takes precedence over filtering (contract §2 step1-bgg-loading row)
  if (input.activeTab === 'bgg' && input.bggSearchQuery.isPending) {
    return { kind: 'step1-bgg-loading', query: trimmedQuery };
  }

  // No search input → default catalog grid
  if (trimmedQuery.length === 0) {
    return {
      kind: 'step1-default',
      catalogResults: input.gameSearchQuery.data ?? [],
      selectedGameId: input.gameId,
    };
  }

  // Search input present + queries settled → check for results
  const catalogResults = input.gameSearchQuery.data ?? [];
  const bggResults = input.bggSearchQuery.data ?? [];
  const totalResults = catalogResults.length + bggResults.length;

  // Catalog tab + 0 catalog results → no-results panel (BGG tab opens BGG search)
  if (input.activeTab === 'catalog' && totalResults === 0) {
    return { kind: 'step1-no-results', query: trimmedQuery };
  }

  // Active filtering with at least 1 result OR BGG tab not loading
  return {
    kind: 'step1-searching',
    query: trimmedQuery,
    activeTab: input.activeTab,
    catalogResults,
    bggResults,
  };
}

function deriveStep2Cell(input: DeriveStateInput): WizardFSMCell {
  // gameId MUST be set on step 2 — orchestrator guards this with redirect.
  // If null we surface step2-denied with synthesized empty gameId for safety.
  const gameId = input.gameId ?? '';

  // Permission denied or unsupported → step2-denied (overrides everything)
  if (input.cameraPermission === 'denied' || input.cameraPermission === 'unsupported') {
    return {
      kind: 'step2-denied',
      gameId,
      permissionState: input.cameraPermission,
    };
  }

  // Mid-shot animation transient
  if (input.isCapturing) {
    return {
      kind: 'step2-capturing',
      gameId,
      capturedCount: input.capturedCount,
    };
  }

  // Light too low → low-light hint
  if (input.lightMeterValue < LIGHT_LOW_THRESHOLD) {
    return {
      kind: 'step2-low-light',
      gameId,
      capturedCount: input.capturedCount,
    };
  }

  // Page detection failed → red dashed frame
  if (input.detectionScore < PAGE_DETECTION_THRESHOLD) {
    return {
      kind: 'step2-failed',
      gameId,
      capturedCount: input.capturedCount,
      detectionScore: input.detectionScore,
    };
  }

  // Happy path — shutter armed (also covers permissionState='prompt' before user grants)
  return {
    kind: 'step2-ready',
    gameId,
    permissionState: input.cameraPermission === 'granted' ? 'granted' : 'prompt',
    capturedCount: input.capturedCount,
  };
}

function deriveStep3Cell(input: DeriveStateInput): WizardFSMCell {
  const batchId = input.batchId ?? '';

  // Offline override (contract §2 step3-offline row) — takes precedence over
  // progress/partial/complete derivation.
  if (input.isOffline) {
    return {
      kind: 'step3-offline',
      batchId,
      retryAttempt: input.retryAttempt,
      nextRetryInMs: input.nextRetryInMs,
    };
  }

  const status = input.batchStatus.data;

  // Status not yet available — fall back to progress with 0/0 (renders skeleton)
  if (!status) {
    return {
      kind: 'step3-progress',
      batchId,
      processedPages: 0,
      totalPages: 0,
    };
  }

  // Completed — split between partial (any low-conf page) and complete (all high)
  if (status.status === 'Completed') {
    const lowConfPages = status.failedPageNumbers ?? [];
    const hasLowConfidence =
      lowConfPages.length > 0 ||
      (status.averageConfidence !== null && status.averageConfidence < LOW_CONFIDENCE_THRESHOLD);

    if (hasLowConfidence) {
      return {
        kind: 'step3-partial',
        batchId,
        processedPages: status.processedPages,
        totalPages: status.totalPages,
        lowConfidencePages: lowConfPages,
      };
    }

    return {
      kind: 'step3-complete',
      batchId,
      totalPages: status.totalPages,
    };
  }

  // Pending or Processing — progress bar
  return {
    kind: 'step3-progress',
    batchId,
    processedPages: status.processedPages,
    totalPages: status.totalPages,
  };
}
