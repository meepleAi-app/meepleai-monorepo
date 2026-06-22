/**
 * buildSpecsItems - Wave C.1 follow-up (Issue #1463)
 *
 * Helper that maps a `LibraryGameDetail` entity to the 8-item presentational
 * shape consumed by `GameDetailSpecsCard`. Encapsulates:
 *   - i18n label resolution via caller-provided `t()` function (react-intl wrapper)
 *   - null cascade handling (rating/complexity/year/age/designer/publisher → '—')
 *   - collection mapping (designers/publishers → first-only, mockup-faithful, D4)
 *   - publisher fallback chain (D5: publishers[0] ?? gamePublisher ?? '—')
 *
 * Mirror of `buildKpiCards()` pattern (`apps/web/src/hooks/queries/useLibrary.ts:230`).
 *
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573); design-handoff PILOT_GAP_REPORT § 2.1
 */

import type { GameDetailSpecsItem } from '@/components/features/game-detail/GameDetailSpecsCard';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

type TranslateFn = (id: string) => string;

const DASH = '—' as const;

export function buildSpecsItems(
  detail: LibraryGameDetail,
  t: TranslateFn
): ReadonlyArray<GameDetailSpecsItem> {
  return [
    {
      key: 'players',
      label: t('pages.gameDetail.info.specsPlayers'),
      value:
        detail.minPlayers != null && detail.maxPlayers != null
          ? detail.minPlayers === detail.maxPlayers
            ? String(detail.minPlayers)
            : `${detail.minPlayers}–${detail.maxPlayers}`
          : DASH,
    },
    {
      key: 'duration',
      label: t('pages.gameDetail.info.specsDuration'),
      value:
        detail.playingTimeMinutes != null
          ? `${detail.playingTimeMinutes} ${t('pages.gameDetail.info.specsMinutesUnit')}`
          : DASH,
    },
    {
      key: 'age',
      label: t('pages.gameDetail.info.specsAge'),
      value: detail.minAge != null ? `${detail.minAge}+` : DASH,
    },
    {
      key: 'complexity',
      label: t('pages.gameDetail.info.specsComplexity'),
      value: detail.complexityRating != null ? `${detail.complexityRating.toFixed(1)} / 5` : DASH,
    },
    {
      key: 'year',
      label: t('pages.gameDetail.info.specsYear'),
      value: detail.gameYearPublished != null ? String(detail.gameYearPublished) : DASH,
    },
    // D4 first-only: mostra primo designer; co-autori non visibili (mockup-faithful)
    {
      key: 'designer',
      label: t('pages.gameDetail.info.specsDesigner'),
      value: detail.designers?.[0]?.name ?? DASH,
    },
    // D5 first-extended primary, denormalized fallback (private games path)
    {
      key: 'publisher',
      label: t('pages.gameDetail.info.specsPublisher'),
      value: detail.publishers?.[0]?.name ?? detail.gamePublisher ?? DASH,
    },
    {
      key: 'rating',
      label: t('pages.gameDetail.info.specsRatingBgg'),
      value: detail.averageRating != null ? detail.averageRating.toFixed(1) : DASH,
    },
  ];
}
