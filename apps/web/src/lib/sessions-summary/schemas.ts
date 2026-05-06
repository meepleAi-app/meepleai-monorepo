/**
 * Stub schemas + fixture-kind enum for `/sessions/[id]` summary (Wave D.3).
 *
 * Schema reality v1 carryover (Gate B):
 *   - `AchievementDto` is a FRONTEND-ONLY stub. No backend `/achievements`
 *     endpoint exists in v1. The `useSessionAchievements` hook (added in
 *     Task 1.1) returns deterministic fixture data — verified via:
 *       grep -rn "/achievements\|AchievementDto" \
 *         apps/api/src/Api/BoundedContexts/SessionTracking/
 *     CONFIRMED stub-only. Backend impl deferred to a future epic.
 *
 *   - `SessionSummaryFixtureKind` is the discriminant for the visual-test
 *     fixture sentinel pattern (mirror Wave D.2 / Wave D.1). The kinds map
 *     to the 6 visual baselines committed by Task 5 (default + 5 variants).
 *
 * Used by:
 *   - `apps/web/src/lib/sessions-summary/visual-test-fixture.ts`
 *   - `apps/web/src/lib/sessions-summary/fsm.ts` (cell input)
 *   - `apps/web/src/components/v2/session-summary/AchievementsCarousel.tsx` (Task 2)
 *   - `apps/web/src/hooks/queries/useSessionAchievements.ts` (Task 1.1, Task 3)
 */

import { z } from 'zod';

/**
 * Stub achievement schema (no backend v1).
 *
 * `unlockedAt` is null when locked. `iconEmoji` is a single graphic cluster
 * suitable for `<span role="img" aria-label={titleKey}>`.
 *
 * `titleKey` and `descriptionKey` are i18n keys, NOT inline strings, so the
 * orchestrator can resolve them via `t()` with locale-aware formatting.
 */
export const achievementSchema = z.object({
  id: z.string(),
  code: z.string(),
  titleKey: z.string(),
  descriptionKey: z.string(),
  iconEmoji: z.string(),
  unlockedAt: z.string().datetime().nullable(),
});

export type AchievementDto = z.infer<typeof achievementSchema>;

/**
 * Discriminant for the 6 visual fixture variants used by the sentinel pattern.
 *
 *   - `default`              → 4-player session, distinct scores, completed
 *   - `tied`                 → 4-player session, top 2 tied at same score
 *   - `abandoned`            → completed status='Abandoned' with abandoned banner
 *   - `solo`                 → 1-player sandbox session
 *   - `empty-achievements`   → full default but achievements=[] (Cell 6 partial)
 *   - `empty-photos`         → full default but snapshots=[] (Cell 6 partial)
 *
 * NOT covered: `error` (TanStack `isError` non-deterministic via URL — covered
 * by integration tests instead, see contract §15.2).
 */
export const sessionSummaryFixtureKindSchema = z.enum([
  'default',
  'tied',
  'abandoned',
  'solo',
  'empty-achievements',
  'empty-photos',
]);

export type SessionSummaryFixtureKind = z.infer<typeof sessionSummaryFixtureKindSchema>;
