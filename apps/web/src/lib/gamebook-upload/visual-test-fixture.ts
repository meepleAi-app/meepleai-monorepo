/**
 * Visual-regression test fixture for `/gamebook/upload` 3-step wizard
 * (SP6 Phase C.1.A Foundation). Mirrors the Wave D.1 / D.2 / D.3 / SP6 Phase B
 * sentinel pattern used by `apps/web/src/lib/gamebook-index/visual-test-fixture.ts`.
 *
 * The workflow `visual-regression-migrated.yml` runs only a Next.js production
 * build (no backend at `:8080`), so the orchestrator (Phase C.1.B Task 3)
 * short-circuits its hooks via `?fixture=` URL override when this fixture
 * file is enabled.
 *
 * Production safety: production builds do NOT set
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED`. The `STATE_OVERRIDE_ENABLED`
 * gate evaluates to its `process.env.NODE_ENV !== 'production'` branch only,
 * which is `false` in prod, allowing the bundler to dead-code-eliminate the
 * `?fixture=` URL hatch. The fixture data itself is also dead code in prod
 * because the orchestrator only reads it when the hatch is enabled.
 *
 * Schema reality v1 carryover (Gate B): see `schemas.ts` header. BGG search
 * shape is aspirational pending API audit; per-page confidence shape is
 * heuristic until backend tracks per-page (deferred per contract §12).
 *
 * State coverage (14 fixture kinds, mirrors FSM cells):
 *   Step 1: step1-default | step1-searching | step1-no-results | step1-bgg-loading
 *   Step 2: step2-ready | step2-capturing | step2-low-light | step2-failed | step2-denied
 *   Step 3: step3-progress | step3-partial | step3-complete | step3-offline | step3-cancel-modal
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp6-gamebook-upload.spec.ts` (Phase C.1.B Task 4)
 *   - `apps/web/e2e/v2-states/gamebook-upload.spec.ts` (Phase C.1.B Task 4)
 */

import { wizardFixtureKindSchema } from './schemas';

import type { WizardStep } from './fsm';
import type {
  BggSearchResult,
  CameraPermissionState,
  CapturedPage,
  CatalogGameRef,
  WizardFixtureKind,
} from './schemas';

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (which sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Build-time gating for the `?fixture=` URL override hatch.
 *
 * Allowing fixture overrides in production would expose a UI manipulation
 * surface. The hatch is enabled only when IS_VISUAL_TEST_BUILD=true OR
 * NODE_ENV !== 'production' (development/test environments).
 */
export const STATE_OVERRIDE_ENABLED = IS_VISUAL_TEST_BUILD || process.env.NODE_ENV !== 'production';

/**
 * Composed fixture record. Exposed shape mirrors what the orchestrator hooks
 * (Phase C.1.B) would collectively return on a happy-path render.
 *
 * The orchestrator reads exactly the fields it needs for the active step —
 * the rest stay defaulted. This keeps the fixture serializable and easy
 * to diff in unit tests.
 */
export interface WizardFixture {
  /** Wizard step indicator (mirrors `?step=`). */
  readonly step: WizardStep;

  /** SSOT for step-1 → step-2 transition (mirrors `?gameId=`). */
  readonly gameId: string | null;

  /** SSOT for step-2 → step-3 transition (mirrors `?batchId=`). */
  readonly batchId: string | null;

  /** Step 1 — catalog search results. */
  readonly catalogResults: readonly CatalogGameRef[];

  /** Step 1 — BGG remote search results. */
  readonly bggResults: readonly BggSearchResult[];

  /** Step 1 — search input text (already debounced for fixture purposes). */
  readonly searchInput: string;

  /** Step 1 — active tab discriminant. */
  readonly activeTab: 'catalog' | 'bgg';

  /** Step 1 — true if BGG remote search is mid-flight. */
  readonly bggIsLoading: boolean;

  /** Step 2 — camera permission matrix (per contract §9). */
  readonly cameraPermission: CameraPermissionState;

  /** Step 2 — light-meter sample 0..1. */
  readonly lightMeterValue: number;

  /** Step 2 — page-detection corner score 0..1. */
  readonly detectionScore: number;

  /** Step 2 — true during shutter mid-shot animation (≤500ms). */
  readonly isCapturing: boolean;

  /** Step 2 — optimistic captured pages. */
  readonly capturedPages: readonly CapturedPage[];

  /** Step 3 — batch status snapshot (null = still loading). */
  readonly batchStatus: WizardBatchStatus | null;

  /** Step 3 — true when offline retry budget is active. */
  readonly isOffline: boolean;

  /** Step 3 — current retry attempt 0..5. */
  readonly retryAttempt: number;

  /** Step 3 — ms remaining until next retry fires. */
  readonly nextRetryInMs: number;

  /** Cross-cutting — true when cancel-modal is visible. */
  readonly cancelModalOpen: boolean;
}

/**
 * Minimal slice of `PhotoBatchStatusSchema` (from `lib/gamebook/schemas.ts`)
 * that the orchestrator passes through to the FSM. Kept inline so this
 * fixture file does not import the consumption-side schema.
 */
export interface WizardBatchStatus {
  readonly status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled';
  readonly totalPages: number;
  readonly processedPages: number;
  readonly averageConfidence: number | null;
  readonly failedPageNumbers?: readonly number[];
}

// ---------------------------------------------------------------------------
// Static building blocks
// ---------------------------------------------------------------------------

const GAME_ID_NANOLITH = '00000000-0000-4000-8000-0000000c0001';
const GAME_ID_BRASS = '00000000-0000-4000-8000-0000000c0002';
const GAME_ID_SPIRIT = '00000000-0000-4000-8000-0000000c0003';
const GAME_ID_WINGSPAN = '00000000-0000-4000-8000-0000000c0004';
const GAME_ID_GLOOMHAVEN = '00000000-0000-4000-8000-0000000c0005';

const BATCH_ID_DEFAULT = '00000000-0000-4000-8000-00000000b001';

/**
 * 5 deterministic catalog game refs. Reused across step-1 fixtures so that
 * mockup parity is consistent. Status mix: 4 indexed + 1 not-yet (Wingspan)
 * to surface the "alreadyIndexed=false" path on the catalog grid.
 */
const CATALOG_DEFAULT: readonly CatalogGameRef[] = [
  {
    id: GAME_ID_NANOLITH,
    title: 'Nanolith',
    publisher: 'Self-published',
    coverImageUrl: null,
    sharedByCount: 142,
    isIndexed: true,
  },
  {
    id: GAME_ID_BRASS,
    title: 'Brass Birmingham',
    publisher: 'Roxley Games',
    coverImageUrl: null,
    sharedByCount: 87,
    isIndexed: true,
  },
  {
    id: GAME_ID_SPIRIT,
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    coverImageUrl: null,
    sharedByCount: 53,
    isIndexed: true,
  },
  {
    id: GAME_ID_WINGSPAN,
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    coverImageUrl: null,
    sharedByCount: 0,
    isIndexed: false,
  },
  {
    id: GAME_ID_GLOOMHAVEN,
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    coverImageUrl: null,
    sharedByCount: 234,
    isIndexed: true,
  },
];

/**
 * 3 deterministic BGG search results for `step1-bgg-loading` follow-up
 * fixture (when BGG returns successfully).
 */
const BGG_RESULTS_DEFAULT: readonly BggSearchResult[] = [
  {
    bggId: 224517,
    title: 'Brass: Birmingham',
    publisher: 'Roxley Games',
    yearPublished: 2018,
  },
  {
    bggId: 162886,
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    yearPublished: 2017,
  },
  {
    bggId: 174430,
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    yearPublished: 2017,
  },
];

/**
 * Empty captured-pages list for fixtures where the user is still framing
 * the first shot (step2-ready, step2-low-light, step2-failed, step2-denied).
 */
const NO_CAPTURED_PAGES: readonly CapturedPage[] = [];

/**
 * 3 captured pages for step2-capturing fixture — mid-loop multi-shot snapshot.
 */
const CAPTURED_PAGES_3: readonly CapturedPage[] = [
  { pageNumber: 1, thumbObjectUrl: 'blob:fixture-1', pendingUpload: false },
  { pageNumber: 2, thumbObjectUrl: 'blob:fixture-2', pendingUpload: false },
  { pageNumber: 3, thumbObjectUrl: 'blob:fixture-3', pendingUpload: true },
];

// ---------------------------------------------------------------------------
// Default fixture (step1-default — used as base for spread)
// ---------------------------------------------------------------------------

const FIXTURE_BASE: WizardFixture = {
  step: 1,
  gameId: null,
  batchId: null,
  catalogResults: CATALOG_DEFAULT,
  bggResults: [],
  searchInput: '',
  activeTab: 'catalog',
  bggIsLoading: false,
  cameraPermission: 'prompt',
  lightMeterValue: 1.0,
  detectionScore: 1.0,
  isCapturing: false,
  capturedPages: NO_CAPTURED_PAGES,
  batchStatus: null,
  isOffline: false,
  retryAttempt: 0,
  nextRetryInMs: 0,
  cancelModalOpen: false,
};

// ---------------------------------------------------------------------------
// Step 1 — Game selection (4 fixtures)
// ---------------------------------------------------------------------------

const FIXTURE_STEP1_DEFAULT: WizardFixture = { ...FIXTURE_BASE };

const FIXTURE_STEP1_SEARCHING: WizardFixture = {
  ...FIXTURE_BASE,
  searchInput: 'spirit',
  catalogResults: [CATALOG_DEFAULT[2]], // only Spirit Island matches
};

const FIXTURE_STEP1_NO_RESULTS: WizardFixture = {
  ...FIXTURE_BASE,
  searchInput: 'flibbertigibbet',
  catalogResults: [],
  bggResults: [],
};

const FIXTURE_STEP1_BGG_LOADING: WizardFixture = {
  ...FIXTURE_BASE,
  searchInput: 'gloomhaven',
  activeTab: 'bgg',
  bggIsLoading: true,
  bggResults: BGG_RESULTS_DEFAULT,
};

// ---------------------------------------------------------------------------
// Step 2 — Photo capture (5 fixtures)
// ---------------------------------------------------------------------------

const FIXTURE_STEP2_READY: WizardFixture = {
  ...FIXTURE_BASE,
  step: 2,
  gameId: GAME_ID_NANOLITH,
  cameraPermission: 'granted',
  lightMeterValue: 0.78,
  detectionScore: 0.95,
};

const FIXTURE_STEP2_CAPTURING: WizardFixture = {
  ...FIXTURE_BASE,
  step: 2,
  gameId: GAME_ID_NANOLITH,
  cameraPermission: 'granted',
  lightMeterValue: 0.78,
  detectionScore: 0.95,
  isCapturing: true,
  capturedPages: CAPTURED_PAGES_3,
};

const FIXTURE_STEP2_LOW_LIGHT: WizardFixture = {
  ...FIXTURE_BASE,
  step: 2,
  gameId: GAME_ID_NANOLITH,
  cameraPermission: 'granted',
  lightMeterValue: 0.18, // <0.3 threshold
  detectionScore: 0.95,
};

const FIXTURE_STEP2_FAILED: WizardFixture = {
  ...FIXTURE_BASE,
  step: 2,
  gameId: GAME_ID_NANOLITH,
  cameraPermission: 'granted',
  lightMeterValue: 0.78,
  detectionScore: 0.2, // <0.5 threshold
};

const FIXTURE_STEP2_DENIED: WizardFixture = {
  ...FIXTURE_BASE,
  step: 2,
  gameId: GAME_ID_NANOLITH,
  cameraPermission: 'denied',
};

// ---------------------------------------------------------------------------
// Step 3 — Indexing progress (5 fixtures incl. cancel-modal)
// ---------------------------------------------------------------------------

const FIXTURE_STEP3_PROGRESS: WizardFixture = {
  ...FIXTURE_BASE,
  step: 3,
  gameId: GAME_ID_NANOLITH,
  batchId: BATCH_ID_DEFAULT,
  batchStatus: {
    status: 'Processing',
    totalPages: 12,
    processedPages: 7,
    averageConfidence: null,
  },
};

const FIXTURE_STEP3_PARTIAL: WizardFixture = {
  ...FIXTURE_BASE,
  step: 3,
  gameId: GAME_ID_NANOLITH,
  batchId: BATCH_ID_DEFAULT,
  batchStatus: {
    status: 'Completed',
    totalPages: 12,
    processedPages: 12,
    averageConfidence: 0.65,
    failedPageNumbers: [3, 7],
  },
};

const FIXTURE_STEP3_COMPLETE: WizardFixture = {
  ...FIXTURE_BASE,
  step: 3,
  gameId: GAME_ID_NANOLITH,
  batchId: BATCH_ID_DEFAULT,
  batchStatus: {
    status: 'Completed',
    totalPages: 12,
    processedPages: 12,
    averageConfidence: 0.92,
    failedPageNumbers: [],
  },
};

const FIXTURE_STEP3_OFFLINE: WizardFixture = {
  ...FIXTURE_BASE,
  step: 3,
  gameId: GAME_ID_NANOLITH,
  batchId: BATCH_ID_DEFAULT,
  batchStatus: {
    status: 'Processing',
    totalPages: 12,
    processedPages: 7,
    averageConfidence: null,
  },
  isOffline: true,
  retryAttempt: 2,
  nextRetryInMs: 4_000,
};

const FIXTURE_STEP3_CANCEL_MODAL: WizardFixture = {
  ...FIXTURE_BASE,
  step: 3,
  gameId: GAME_ID_NANOLITH,
  batchId: BATCH_ID_DEFAULT,
  batchStatus: {
    status: 'Processing',
    totalPages: 12,
    processedPages: 5,
    averageConfidence: null,
  },
  cancelModalOpen: true,
};

// ---------------------------------------------------------------------------
// Single source of truth (14 fixture variants)
// ---------------------------------------------------------------------------

/**
 * 14 fixture variants keyed by `WizardFixtureKind`. The discriminant is
 * also used by the `?fixture=` URL override.
 */
export const wizardFixtures: Readonly<Record<WizardFixtureKind, WizardFixture>> = {
  'step1-default': FIXTURE_STEP1_DEFAULT,
  'step1-searching': FIXTURE_STEP1_SEARCHING,
  'step1-no-results': FIXTURE_STEP1_NO_RESULTS,
  'step1-bgg-loading': FIXTURE_STEP1_BGG_LOADING,
  'step2-ready': FIXTURE_STEP2_READY,
  'step2-capturing': FIXTURE_STEP2_CAPTURING,
  'step2-low-light': FIXTURE_STEP2_LOW_LIGHT,
  'step2-failed': FIXTURE_STEP2_FAILED,
  'step2-denied': FIXTURE_STEP2_DENIED,
  'step3-progress': FIXTURE_STEP3_PROGRESS,
  'step3-partial': FIXTURE_STEP3_PARTIAL,
  'step3-complete': FIXTURE_STEP3_COMPLETE,
  'step3-offline': FIXTURE_STEP3_OFFLINE,
  'step3-cancel-modal': FIXTURE_STEP3_CANCEL_MODAL,
};

// ---------------------------------------------------------------------------
// URL override parsing
// ---------------------------------------------------------------------------

/**
 * Parses the `?fixture=` URL search param into a `WizardFixtureKind`.
 *
 * Returns `null` when:
 *   - `STATE_OVERRIDE_ENABLED` is false (production builds without the env var)
 *   - The param is missing, empty, or not a recognized fixture kind
 *
 * Accepts either a `URLSearchParams`, a plain string (the param value), or
 * `null`/`undefined` for ergonomic call sites.
 */
export function parseStateOverride(
  searchParam: string | URLSearchParams | null | undefined
): WizardFixtureKind | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  let raw: string | null;
  if (searchParam == null) {
    raw = null;
  } else if (typeof searchParam === 'string') {
    raw = searchParam;
  } else {
    raw = searchParam.get('fixture');
  }
  if (!raw) return null;
  const parsed = wizardFixtureKindSchema.safeParse(raw);
  return parsed.success ? parsed.data : null;
}
