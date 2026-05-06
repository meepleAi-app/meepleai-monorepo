/**
 * Barrel re-exports for `/gamebook/upload` 3-step wizard lib
 * (SP6 Phase C.1.A Foundation).
 *
 * Consumed by:
 *   - 11 v2 components in `apps/web/src/components/v2/gamebook-upload/` (Phase C.1 Tasks 2/B)
 *   - Orchestrator `GamebookUploadView.tsx` (Phase C.1 Tasks 3/B)
 *   - E2E specs in `apps/web/e2e/` (Phase C.1 Tasks 4/B)
 *   - Phase C.1.B Interactions side-effect modules (camera-capabilities, offline-budget,
 *     confidence-classifier, api-extension)
 */

// FSM
export {
  deriveWizardState,
  LIGHT_LOW_THRESHOLD,
  LOW_CONFIDENCE_THRESHOLD,
  PAGE_DETECTION_THRESHOLD,
  type DeriveStateInput,
  type QueryLike,
  type WizardFSMCell,
  type WizardStep,
} from './fsm';

// Schemas
export {
  BggSearchResultSchema,
  CameraPermissionStateSchema,
  CapturedPageSchema,
  CatalogGameRefSchema,
  ConfidenceLevelSchema,
  GameSearchTabSchema,
  IndexedPageMetaSchema,
  LightMeterReadingSchema,
  NoResultsActionSchema,
  RETRY_BUDGET_TOTAL_MS,
  RETRY_DELAYS_MS,
  RetryStateSchema,
  wizardFixtureKindSchema,
  type BggSearchResult,
  type CameraPermissionState,
  type CapturedPage,
  type CatalogGameRef,
  type ConfidenceLevel,
  type GameSearchTab,
  type IndexedPageMeta,
  type LightMeterReading,
  type NoResultsAction,
  type RetryState,
  type WizardFixtureKind,
} from './schemas';

// Visual test fixture
export {
  IS_VISUAL_TEST_BUILD,
  parseStateOverride,
  STATE_OVERRIDE_ENABLED,
  wizardFixtures,
  type WizardBatchStatus,
  type WizardFixture,
} from './visual-test-fixture';

// Idempotency-key composer
export {
  composeIdempotencyKey,
  parseIdempotencyKey,
  type IdempotencyKeyParts,
} from './idempotency-key';
