/**
 * Hook: useEntityNavigation
 *
 * Resolves navigation links for a MeepleCard entity from the central
 * navigation graph configuration.
 *
 * @see config/entity-navigation.ts
 * @see Issue #4690
 */

import { useMemo } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import {
  type EntityIdBag,
  type ResolvedNavigationLink,
  getNavigationLinks,
} from '@/config/entity-navigation';

/**
 * Returns resolved navigation links for a given entity and its id bag.
 *
 * Links whose required id is missing from `entityData` are automatically
 * filtered out, so the returned array is always "ready to render".
 *
 * ```tsx
 * const links = useEntityNavigation('game', { id: game.id });
 * // → [{ entity: 'document', label: 'KB', href: '/games/123/knowledge-base' }, …]
 * ```
 */
export function useEntityNavigation(
  entity: MeepleEntityType,
  entityData: EntityIdBag,
): ResolvedNavigationLink[] {
  return useMemo(
    () => getNavigationLinks(entity, entityData),
    // Stringify the bag so we get stable deps without requiring callers to
    // memoise the object themselves.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [entity, JSON.stringify(entityData)],
  );
}
