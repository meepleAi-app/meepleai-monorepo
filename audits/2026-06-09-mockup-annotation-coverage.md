# Mockup Annotation Coverage — DS-17-1

| Field | Value |
|---|---|
| **Date** | 2026-10-03 |
| **Generator** | `pnpm mockup-annotations:audit` (DS-17-1) |
| **Spec** | [`2026-06-09-mockup-to-app-drift-spec-panel-review.md`](../docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md) |
| **Marker** | `MOCKUP-ANNOTATION` |
| **Denominator** | `mappable` |
| **Coverage** | 0% — 0 / 3 |
| **Status** | below threshold (< 80%) — pass marker absent |

## Uncovered routes

Routes missing the `@mockup` JSDoc block. Run `pnpm mockup-annotations:inject --apply` after extending MOCKUPS_INDEX.md.

| File |
|---|
| `src/app/(authenticated)/library/wishlist/page.tsx` |
| `src/app/(authenticated)/sessions/[id]/live/page.tsx` |
| `src/app/(authenticated)/sessions/[id]/page.tsx` |

## Refs

- Sub-issue: [#2069](https://github.com/meepleAi-app/meepleai-monorepo/issues/2069)
- Umbrella:  [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Plan:      [`2026-06-09-ds-17-phase-1-implementation-plan.md`](../docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md) §4.3
- Companion JSON: [`2026-06-09-mockup-annotation-coverage.json`](./2026-06-09-mockup-annotation-coverage.json)
