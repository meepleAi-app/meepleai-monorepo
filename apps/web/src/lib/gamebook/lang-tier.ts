/**
 * Confidence tier classification per DEC-FE-2 (#1559).
 *
 *   - null/undefined → 'none' (no detection, legacy path or out-of-allowlist null+high)
 *   - confidence > 0.8 → 'high' (informational badge, no friction)
 *   - 0.5 ≤ confidence ≤ 0.8 → 'medium' (tap-to-confirm pill, SegmentPicker blocked)
 *   - confidence < 0.5 → 'low' (auto-open modal, SegmentPicker blocked)
 *
 * Confidence values come from BE NTextCat tanh(relativeGap × 200) normalization
 * (PR #1787 BE deviation note). FE treats the [0,1] range as opaque ordinal.
 */
export type LangTier = 'high' | 'medium' | 'low' | 'none';

export function getLangTier(confidence: number | null | undefined): LangTier {
  if (confidence === null || confidence === undefined) return 'none';
  if (confidence > 0.8) return 'high';
  if (confidence >= 0.5) return 'medium';
  return 'low';
}
