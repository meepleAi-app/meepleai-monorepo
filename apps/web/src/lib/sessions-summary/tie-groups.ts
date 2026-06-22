/**
 * Pure tie-group computation for the `/sessions/[id]` summary podium and
 * scoring breakdown table (Wave D.3, Phase 0.5 contract §12).
 *
 * Algorithm:
 *   1. Sort participants by `totalScore` DESC.
 *   2. Stable secondary sort by `displayName` ASC, locale-aware
 *      (`localeCompare(b, 'it', { sensitivity: 'base' })`) so accented
 *      characters fold to base letters (`É` ≡ `E`) for tie-breaker
 *      determinism.
 *   3. Walk the sorted list; whenever score changes from previous, the
 *      next rank is `previousRank + groupSize`. This produces standard
 *      competition ranking ("1224"): scores [10, 10, 8, 7] → ranks [1, 1, 3, 4].
 *   4. For each participant, `tiedPlayerIds` includes all peer ids that
 *      share the same score (including self). `isTied` is true iff that
 *      list has ≥2 entries.
 *
 * Schema reality v1 carryover (Gate B):
 *   - `ParticipantDto` (from `session-tracking.schemas.ts`) has the fields
 *     we need: `id`, `displayName`, `totalScore`. No `color`, `avatarUrl`,
 *     or `tiedWith` exists at the backend — `tiedPlayerIds` is a frontend-
 *     only computation surfaced via `RankedParticipant`.
 *   - The contract example calls these fields different things in places
 *     (`displayName` vs `name`); we use the canonical `displayName` from
 *     the actual `ParticipantDto` schema.
 *
 * Used by:
 *   - `apps/web/src/components/features/session-summary/SummaryHeroPodium.tsx` (Task 2)
 *   - `apps/web/src/components/features/session-summary/ScoringBreakdownTable.tsx` (Task 2)
 *   - `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` (Task 3)
 */

import type { ParticipantDto } from '@/lib/api/schemas/session-tracking.schemas';

/**
 * Decorated participant after tie-group computation.
 *
 * Extends the existing `ParticipantDto` with three frontend-only fields:
 *   - `rank`           — 1-indexed display rank, with ties sharing the same value.
 *   - `isTied`         — true when ≥2 peers share `rank`.
 *   - `tiedPlayerIds`  — readonly list of all participant ids at this rank
 *                       (always includes self; length 1 when `isTied=false`).
 */
export interface RankedParticipant extends ParticipantDto {
  /** 1-indexed display rank (with ties sharing the same rank). */
  readonly rank: number;
  /** True if 2+ participants share this rank. */
  readonly isTied: boolean;
  /** All participant IDs that share this rank (including self). Length ≥ 1. */
  readonly tiedPlayerIds: readonly string[];
}

/**
 * Compute ranked participants from raw `ParticipantDto` array.
 *
 * Sort order:
 *   1. `totalScore` DESC (primary)
 *   2. `displayName` ASC, locale='it' with `sensitivity: 'base'` (secondary,
 *      tie-breaker for stable ordering — accent-insensitive so `Étienne`
 *      and `Etienne` collate at the same position).
 *
 * The original input array is NOT mutated.
 *
 * Edge cases:
 *   - Empty array  → returns `[]`
 *   - Single entry → returns `[{ ...p, rank: 1, isTied: false, tiedPlayerIds: [p.id] }]`
 *   - All tied     → all get `rank=1`, `isTied=true`, `tiedPlayerIds=[all ids]`
 *
 * Examples (standard competition ranking):
 *   - [42, 36, 28, 19] → ranks [1, 2, 3, 4]
 *   - [42, 42, 28, 19] → ranks [1, 1, 3, 4]    (skip rank 2, two-way tie at 1st)
 *   - [42, 28, 28, 28, 19] → ranks [1, 2, 2, 2, 5]  (three-way tie at 2nd)
 *   - [10, 5, 5] → ranks [1, 2, 2]              (tied at last place)
 *   - [5, 5, 5] → ranks [1, 1, 1]               (all tied)
 */
export function computeRankedParticipants(
  participants: readonly ParticipantDto[]
): readonly RankedParticipant[] {
  if (participants.length === 0) return [];

  // Stable, locale-aware sort. Spread first so we don't mutate the input.
  const sorted = [...participants].sort((a, b) => {
    if (b.totalScore !== a.totalScore) return b.totalScore - a.totalScore;
    return a.displayName.localeCompare(b.displayName, 'it', { sensitivity: 'base' });
  });

  // Pre-compute tied id lists per unique score so we don't do O(n²) per element.
  // Using a plain object keyed by stringified score (Number keys would coerce).
  const idsByScore = new Map<number, string[]>();
  for (const p of sorted) {
    const list = idsByScore.get(p.totalScore);
    if (list) {
      list.push(p.id);
    } else {
      idsByScore.set(p.totalScore, [p.id]);
    }
  }

  // Standard competition ranking: when score changes, the next rank is
  // `position + 1` (1-indexed). Equal scores share the previous rank.
  const result: RankedParticipant[] = [];
  let currentRank = 1;
  for (let i = 0; i < sorted.length; i += 1) {
    const p = sorted[i];
    if (i > 0 && sorted[i - 1].totalScore !== p.totalScore) {
      currentRank = i + 1;
    }
    const tiedPlayerIds = idsByScore.get(p.totalScore) ?? [p.id];
    result.push({
      ...p,
      rank: currentRank,
      isTied: tiedPlayerIds.length > 1,
      tiedPlayerIds,
    });
  }

  return result;
}
