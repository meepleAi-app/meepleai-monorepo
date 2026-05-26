/**
 * Barrel re-exports for `/gamebook` index lib (SP6 Phase B Task 1).
 *
 * Consumed by:
 *   - 5 feature components in `apps/web/src/components/features/gamebook/` (Task 2)
 *   - Orchestrator `GamebookIndexView.tsx` (Task 3)
 *   - E2E specs in `apps/web/e2e/` (Task 4)
 */

export {
  deriveGamebookIndexState,
  QUOTA_SOFT_RATIO,
  type DeriveStateInput,
  type GamebookIndexFSMCell,
  type QueryLike,
} from './fsm';

export {
  gamebookCardDataSchema,
  gamebookIndexFixtureKindSchema,
  gamebookStatusSchema,
  quotaInfoSchema,
  type GamebookCardData,
  type GamebookIndexFixtureKind,
  type GamebookStatus,
  type QuotaInfo,
} from './schemas';

export {
  IS_VISUAL_TEST_BUILD,
  STATE_OVERRIDE_ENABLED,
  gamebookIndexFixtures,
  parseStateOverride,
  type GamebookIndexFixture,
} from './visual-test-fixture';
