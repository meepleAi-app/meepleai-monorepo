'use client';

/**
 * useEntityLinkGroups — derived hook for grouped entity link counts
 *
 * Pure function `groupEntityLinks` groups EntityLinkDto[] by targetEntityType.
 * Hook `useEntityLinkGroups` wraps useEntityLinks + groupEntityLinks.
 *
 * Used by CompactIconBar to show which entity type icons and their counts.
 *
 * Issue #448 — Service Health Monitoring
 */

import { useMemo } from 'react';

import { useEntityLinks } from './use-entity-links';

import type { EntityLinkDto, LinkEntityType } from './entity-link-types';

export interface EntityLinkGroup {
  entityType: LinkEntityType;
  count: number;
  firstTargetName?: string;
}

/**
 * Groups an array of entity links by their targetEntityType.
 *
 * @param links - Array of EntityLinkDto to group
 * @returns Array of EntityLinkGroup with counts and optional first target name
 */
export function groupEntityLinks(links: EntityLinkDto[]): EntityLinkGroup[] {
  const map = new Map<LinkEntityType, { count: number; firstTargetName?: string }>();

  for (const link of links) {
    const existing = map.get(link.targetEntityType);
    if (existing) {
      existing.count++;
    } else {
      map.set(link.targetEntityType, {
        count: 1,
        firstTargetName: link.metadata ?? undefined,
      });
    }
  }

  return Array.from(map.entries()).map(([entityType, data]) => ({
    entityType,
    count: data.count,
    firstTargetName: data.firstTargetName,
  }));
}

/**
 * React hook that fetches entity links and returns them grouped by target entity type.
 *
 * @param entityType - The source entity type
 * @param entityId - The source entity ID
 * @returns Grouped links, loading state, and error
 */
export function useEntityLinkGroups(entityType: LinkEntityType, entityId: string) {
  const { links, loading, error } = useEntityLinks(entityType, entityId);
  const groups = useMemo(() => groupEntityLinks(links), [links]);
  return { groups, loading, error };
}
