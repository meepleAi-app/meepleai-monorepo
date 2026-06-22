/**
 * Toolkit Ratings Schemas (Wave 3 Phase 4b, Issue #805 / PR #732 §5.3.3)
 *
 * Zod schemas for the SP4 `/toolkits/[id]` ratings tab. Backend contract:
 *   - `GET /api/v1/toolkits/{toolkitId}/ratings?cursor={nullable}&limit=20`
 *
 * Response follows the PR #732 §3.4 empty-state contract: shape is always
 * `{ items, nextCursor, breakdown, averageStars, totalCount }` and an empty
 * list is a 200 (not 404 / 204).
 *
 * Schema reality v1 carryover (Gate B): the `ToolkitRating` entity does not
 * exist yet. Once toolkit visibility passes, the backend returns:
 *   - `items: []`
 *   - `nextCursor: null`
 *   - `breakdown: { star1..star5: 0 }`
 *   - `averageStars: 0`
 *   - `totalCount: 0`
 * Wire shape is stable so the ratings tab can render today and adopt real
 * data without a fetch shape change in Phase 5.
 *
 * The companion `POST /toolkits/{id}/ratings` returns 501 in Phase 4b — no
 * FE submission hook ships in this PR; the form can show "coming soon"
 * affordances without an API call until Phase 5.
 */

import { z } from 'zod';

export const ToolkitRatingsBreakdownSchema = z.object({
  star1: z.number().int().nonnegative(),
  star2: z.number().int().nonnegative(),
  star3: z.number().int().nonnegative(),
  star4: z.number().int().nonnegative(),
  star5: z.number().int().nonnegative(),
});
export type ToolkitRatingsBreakdown = z.infer<typeof ToolkitRatingsBreakdownSchema>;

export const ToolkitRatingSchema = z.object({
  id: z.string().uuid(),
  raterDisplayName: z.string().min(1),
  raterAvatarUrl: z.string().nullable(),
  stars: z.number().int().min(1).max(5),
  comment: z.string().nullable(),
  createdAt: z.string(),
});
export type ToolkitRating = z.infer<typeof ToolkitRatingSchema>;

export const ToolkitRatingsResponseSchema = z.object({
  items: z.array(ToolkitRatingSchema),
  nextCursor: z.string().nullable(),
  breakdown: ToolkitRatingsBreakdownSchema,
  averageStars: z.number().nonnegative(),
  totalCount: z.number().int().nonnegative(),
});
export type ToolkitRatingsResponse = z.infer<typeof ToolkitRatingsResponseSchema>;
