/**
 * Barrel re-exports for `/sessions/[id]` summary lib (Wave D.3).
 *
 * Consumed by:
 *   - 11 v2 components in `apps/web/src/components/v2/session-summary/` (Task 2)
 *   - Orchestrator `SessionSummaryView.tsx` (Task 3)
 *   - E2E specs in `apps/web/e2e/` (Task 4)
 */

export {
  deriveSessionSummaryState,
  type DeriveStateInput,
  type QueryLike,
  type SessionSummaryFSMCell,
} from './fsm';

export { computeRankedParticipants, type RankedParticipant } from './tie-groups';

export { shouldShowConfetti, clearConfettiFlag } from './confetti-trigger';

export {
  achievementSchema,
  sessionSummaryFixtureKindSchema,
  type AchievementDto,
  type SessionSummaryFixtureKind,
} from './schemas';

export {
  IS_VISUAL_TEST_BUILD,
  SESSION_SUMMARY_VISUAL_TEST_SENTINEL,
  STATE_OVERRIDE_ENABLED,
  parseStateOverride,
  sessionSummaryFixtures,
  type SessionSummaryFixture,
} from './visual-test-fixture';

export {
  ENTITY_TEXT_HSL,
  entityBorderHslClass,
  entityTextHslClass,
  type SessionSummaryEntity,
} from './entity-text-tokens';
