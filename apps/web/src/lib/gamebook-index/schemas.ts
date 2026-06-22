/**
 * Schema definitions for `/gamebook` index (SP6 Phase B Task 1).
 *
 * Schema reality v1 carryover (Gate B):
 *   - The `useGamebooks` hook DOES NOT EXIST on main-dev. The backend
 *     `GET /api/v1/gamebooks` endpoint is NOT exposed. Verified via:
 *       grep -rn "useGamebooks\|getMyGamebooks\|gamebooks:" \
 *         apps/web/src/hooks/queries/ apps/web/src/lib/
 *       grep -rn "MapGet.*gamebook\|MapGet.*GameBook" \
 *         apps/api/src/Api/Routing/
 *     Decision per plan Pre-3 audit: stub via fixture for Phase B; real hook
 *     integration deferred to Phase B Task 3 (orchestrator) where an adapter
 *     will wrap `getMyGames` filtered by `hasGamebook` flag (or similar
 *     fallback). Backend endpoint exposure deferred to follow-up issue.
 *
 *   - The fields on `GamebookCardData` (pages, totalPages, chunks, qaCount,
 *     sessionsCount, errorMsg) are aspirational — backend does not expose all
 *     these fields yet. The Phase B Task 3 orchestrator adapter will handle
 *     the real shape and project a `GamebookCardData`-shaped value from
 *     whatever the backend actually returns.
 *
 *   - `QuotaInfo` schema has NO matching backend endpoint in v1. The
 *     orchestrator will stub quota data via fixture (Gate B documented).
 *     Backend `GET /api/v1/users/me/quota` (or equivalent) deferred to
 *     follow-up issue.
 *
 * Used by:
 *   - `apps/web/src/lib/gamebook-index/fsm.ts` (cell input)
 *   - `apps/web/src/lib/gamebook-index/visual-test-fixture.ts`
 *   - `apps/web/src/components/features/gamebook/*` (Task 2)
 *   - `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx` (Task 3)
 */

import { z } from 'zod';

/**
 * Gamebook processing status. Mirrors the SP6 mockup `StatusPill`:
 *   - `ready`    → green pill, fully indexed and queryable
 *   - `indexing` → orange pill, OCR/RAG pipeline running (X/Y pages done)
 *   - `error`    → red pill, OCR failed (errorMsg explains why)
 */
export const gamebookStatusSchema = z.enum(['ready', 'indexing', 'error']);
export type GamebookStatus = z.infer<typeof gamebookStatusSchema>;

/**
 * Single gamebook card data shape used by `GamebookCard` and the FSM cells.
 *
 * Field semantics:
 *   - `pages`        Pages successfully indexed (≤ totalPages)
 *   - `totalPages`   Total pages discovered by OCR pipeline
 *   - `chunks`       Number of RAG chunks generated (only meaningful when ready)
 *   - `qaCount`      Number of Q&A interactions executed against this gamebook
 *   - `sessionsCount` Number of game sessions referencing this gamebook
 *   - `cover`        Cover image URL (nullable — fallback to emoji when null)
 *   - `emoji`        Fallback emoji rendered when cover is null
 *   - `errorMsg`     Human-readable error message (only when status='error')
 */
export const gamebookCardDataSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  title: z.string(),
  publisher: z.string().nullable(),
  year: z.number().int().nullable(),
  pages: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
  chunks: z.number().int().nonnegative(),
  status: gamebookStatusSchema,
  cover: z.string().url().nullable(),
  emoji: z.string().nullable(),
  qaCount: z.number().int().nonnegative(),
  sessionsCount: z.number().int().nonnegative(),
  errorMsg: z.string().nullable(),
});
export type GamebookCardData = z.infer<typeof gamebookCardDataSchema>;

/**
 * Quota envelope for monthly translation budget.
 *
 *   - `used`       Number of paragraphs translated this period
 *   - `total`      Period budget (50 for free tier, larger for premium)
 *   - `resetDate`  ISO 8601 timestamp when the next reset occurs
 *   - `tier`       Pricing tier — affects the `quota.upgrade` CTA visibility
 *
 * Soft warning threshold: used / total ≥ 0.9 (≥90%)
 * Hard limit threshold:    used >= total (==100%)
 */
export const quotaInfoSchema = z.object({
  used: z.number().int().nonnegative(),
  total: z.number().int().positive(),
  resetDate: z.string().datetime(),
  tier: z.enum(['free', 'premium']).default('free'),
});
export type QuotaInfo = z.infer<typeof quotaInfoSchema>;

/**
 * Discriminant for the visual-fixture sentinel pattern. Mirrors the 6 FSM
 * cells exposed by `deriveGamebookIndexState`.
 *
 *   - `default`     → 4 ready/indexing/error gamebooks, quota OK
 *   - `empty`       → 0 gamebooks, quota OK
 *   - `quota-soft`  → ≥1 gamebook, quota ≥90% used
 *   - `quota-hard`  → ≥1 gamebook, quota 100% used (banner visible)
 *   - `loading`     → skeleton render (no gamebooks displayed)
 *   - `error`       → global error state (no gamebooks displayed)
 */
export const gamebookIndexFixtureKindSchema = z.enum([
  'default',
  'empty',
  'quota-soft',
  'quota-hard',
  'loading',
  'error',
]);

export type GamebookIndexFixtureKind = z.infer<typeof gamebookIndexFixtureKindSchema>;
