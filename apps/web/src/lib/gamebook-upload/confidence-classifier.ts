/**
 * Confidence-based retake classifier for `/gamebook/upload` Step 3
 * (SP6 Phase C.2 Task A — Interactions sub-PR).
 *
 * Implements the 3-bucket classification from contract §12:
 *
 *   | Level    | Range     | UX                                                     |
 *   |----------|-----------|--------------------------------------------------------|
 *   | `high`   | ≥0.8      | ✓ green badge, auto-accept, no action prompt          |
 *   | `medium` | 0.5..0.8  | ◐ amber badge, manual review allowed, retake hidden   |
 *   | `low`    | <0.5      | ⚠ red badge, retake CTA visible + thumb red border    |
 *
 * `null` input represents "still processing" (OCR pending) — consumer renders
 * a neutral skeleton placeholder rather than any of the 3 confidence badges.
 *
 * Schema reality v1 carryover (Gate B): backend currently exposes only batch-
 * level `averageConfidence`. The optional `deriveHeuristicPageConfidence`
 * helper distributes batch-avg deterministically across pages until per-page
 * tracking lands (deferred per §12). Heuristic is documented as approximate;
 * when backend exposes per-page conf, classifier consumes real data via
 * `IndexedPageMetaSchema.confidence`.
 *
 * Used by:
 *   - `lib/gamebook-upload/hooks/usePageConfidence.ts` (Phase C.2 Task B)
 *   - `components/v2/gamebook-upload/PageConfidenceBadge.tsx` (Phase C.2 Task B)
 *   - Orchestrator step3 cell derivation (Phase C.2 Task C)
 */

import type { ConfidenceLevel } from './schemas';

/**
 * Score ≥ this threshold maps to `high` confidence.
 * Per contract §12 thresholds.
 */
export const CONFIDENCE_HIGH_THRESHOLD = 0.8;

/**
 * Score ≥ this threshold (but < HIGH) maps to `medium` confidence.
 * Per contract §12 thresholds.
 */
export const CONFIDENCE_MEDIUM_THRESHOLD = 0.5;

/**
 * Classifies a per-page confidence score into the discrete level used for UI
 * rendering (badge color, retake CTA visibility, page thumb border).
 *
 *   - `null` → `null` (still processing — render skeleton)
 *   - score ≥ 0.8 → `high`   (auto-accept)
 *   - 0.5 ≤ score < 0.8 → `medium` (manual review allowed)
 *   - score < 0.5 → `low`    (auto-flag retake)
 *
 * Defensive behavior:
 *   - `NaN` → `low` (degrades safely — surfaces retake CTA on garbage input)
 *   - `-Infinity` → `low`
 *   - `+Infinity` → `high` (passes ≥0.8 threshold)
 *   - Out-of-range > 1 → `high` (no upper clamp; caller responsibility)
 *   - Out-of-range < 0 → `low`
 *
 * @example
 *   classifyConfidence(0.92) // 'high'
 *   classifyConfidence(0.65) // 'medium'
 *   classifyConfidence(0.30) // 'low'
 *   classifyConfidence(null) // null  (processing)
 *   classifyConfidence(NaN)  // 'low' (defensive)
 */
export function classifyConfidence(score: number | null): ConfidenceLevel | null {
  if (score === null) return null;

  // NaN comparison is always false — explicit short-circuit avoids surprise
  if (Number.isNaN(score)) return 'low';

  if (score >= CONFIDENCE_HIGH_THRESHOLD) return 'high';
  if (score >= CONFIDENCE_MEDIUM_THRESHOLD) return 'medium';
  return 'low';
}

/**
 * True iff a page should be auto-flagged for retake (low confidence). Sugar
 * over `classifyConfidence` for callsites that only care about the binary
 * "render retake CTA?" decision. `null` (processing) returns false — retake
 * CTA must NOT appear while OCR is still running.
 *
 * @example
 *   shouldRequestRetake(0.30) // true
 *   shouldRequestRetake(0.65) // false (medium — user-driven retake only)
 *   shouldRequestRetake(0.92) // false
 *   shouldRequestRetake(null) // false (still processing)
 *   shouldRequestRetake(NaN)  // true  (defensive)
 */
export function shouldRequestRetake(score: number | null): boolean {
  return classifyConfidence(score) === 'low';
}

/**
 * Heuristic distribution of batch-level `averageConfidence` across pages
 * pending real per-page tracking from backend (deferred per contract §12).
 *
 * **Limitation**: this is an interim approximation, not ground truth. When
 * backend exposes per-page confidence (Phase 3 Task 3.5b), callers MUST
 * switch to `IndexedPageMetaSchema.confidence` directly and stop using this
 * heuristic. Gate B (schema reality) enforces deletion at that point.
 *
 * Distribution rules:
 *
 *   - `failCount === 0` → all pages reported as `batchAvg` (uniform)
 *   - `failCount > 0` → first `failCount` pages reported as 0.4 (low conf),
 *     remaining pages boosted to `min(1, batchAvg + 0.1)`
 *
 * Rationale: backend `averageConfidence` is computed across ALL pages in the
 * batch, including failed ones. If `failCount > 0`, the batch-avg under-
 * represents the success cases — the +0.1 boost approximates this lift. The
 * 0.4 floor for fails ensures classifier returns `low` (triggers retake CTA).
 *
 * @param pageIndex 0-based page index within the batch
 * @param totalPages total pages in the batch (currently unused — reserved for
 *   future weighted distribution; kept in signature for API stability)
 * @param batchAvg batch-level `averageConfidence` (0..1)
 * @param failCount number of pages with OCR failures in this batch
 *
 * @example
 *   // Uniform distribution (no failures)
 *   deriveHeuristicPageConfidence(0, 12, 0.85, 0) // 0.85
 *   deriveHeuristicPageConfidence(5, 12, 0.85, 0) // 0.85
 *
 *   // Bimodal distribution (3 failures in 12-page batch)
 *   deriveHeuristicPageConfidence(0, 12, 0.70, 3) // 0.4  (failed)
 *   deriveHeuristicPageConfidence(2, 12, 0.70, 3) // 0.4  (failed)
 *   deriveHeuristicPageConfidence(3, 12, 0.70, 3) // 0.80 (boosted)
 */
export function deriveHeuristicPageConfidence(
  pageIndex: number,
  totalPages: number,
  batchAvg: number,
  failCount: number
): number {
  // totalPages is reserved for future use — silence linter without changing API
  void totalPages;

  if (failCount === 0) return batchAvg;
  if (pageIndex < failCount) return 0.4;
  return Math.min(1, batchAvg + 0.1);
}
