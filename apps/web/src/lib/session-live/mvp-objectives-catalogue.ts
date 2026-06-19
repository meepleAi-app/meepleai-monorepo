/**
 * MVP placeholder objectives catalogue.
 *
 * Issue #2389 Block B (T1): hoisted out of `scores/page.tsx` so the read-only
 * `ScoringPanelRenderer` adapter and the mutable `PolymorphicScoreEditor` share
 * the same labels until real game-level catalogue wiring ships
 * (tracked follow-up: replace this stub with a per-game lookup).
 *
 * String entries double as objective IDs (id = label) — the adapter and editor
 * both rely on this identity. When the real catalogue arrives, IDs and labels
 * will diverge (id = GUID, label = i18n key).
 *
 * Do NOT modify entries without coordinating with the editor + renderer tests.
 */
export const MVP_OBJECTIVES_CATALOGUE: readonly string[] = [
  'Vittoria',
  'Sopravvivenza',
  'Tesoro',
  'Boss',
  'Quest',
] as const;
