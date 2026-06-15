import type { RowItemBase } from './HorizontalRow';

/**
 * Maps a Discover row + item to its detail-page href.
 *
 * Resolves #2085 F2 (card click no navigation) + F3 (KB row click no navigation).
 * Before this helper landed, `handleCardClick` in `DiscoverHub.tsx` emitted only
 * telemetry; cards rendered as `<button>` with no `<Link href>` semantics.
 *
 * The 7 rowIds align with `EntityFilter | 'trending'` from `HorizontalRow.tsx`.
 * Returning `null` is the defensive default for unknown rowIds OR empty ids —
 * callers must skip the navigation step (telemetry already fired).
 *
 * KB row maps to `/knowledge-base/{id}` (the canonical KB document detail route)
 * rather than `/library/{gameId}?tab=knowledge-base` because `RowItemBase` from
 * the recent-kb-docs mapper does not expose `gameId` (only `gameName`). The
 * route is the same entity surface tracked by #2223 (sp4-kb-detail forward
 * refactor) — see that issue for the layout iteration.
 */
export function resolveCardHref(rowId: string, item: RowItemBase): string | null {
  if (!item.id) return null;

  const safeId = encodeURIComponent(item.id);

  switch (rowId) {
    case 'trending':
    case 'games':
      return `/games/${safeId}`;
    case 'agents':
      return `/agents/${safeId}`;
    case 'toolkits':
      return `/toolkits/${safeId}`;
    case 'kbs':
      return `/knowledge-base/${safeId}`;
    case 'people':
      return `/players/${safeId}`;
    case 'events':
      return `/game-nights/${safeId}`;
    default:
      return null;
  }
}
