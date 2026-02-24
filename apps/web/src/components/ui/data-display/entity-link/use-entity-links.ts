'use client';

/**
 * useEntityLinks — shared hook
 *
 * Fetches EntityLinks for a given source entity.
 * Used by RelatedEntitiesSection (C5) and EntityRelationshipGraph (C7).
 *
 * Issue #5129 — Epic C
 */

import { useState, useEffect, useCallback } from 'react';

import type { EntityLinkDto, LinkEntityType } from './entity-link-types';

export interface UseEntityLinksResult {
  links: EntityLinkDto[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useEntityLinks(entityType: LinkEntityType, entityId: string): UseEntityLinksResult {
  const [links, setLinks] = useState<EntityLinkDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLinks = useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const params = new URLSearchParams({ entityType, entityId });
        const res = await fetch(`/api/v1/library/entity-links?${params.toString()}`, { signal });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as EntityLinkDto[];
        setLinks(Array.isArray(data) ? data : []);
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Failed to load links');
      } finally {
        setLoading(false);
      }
    },
    [entityType, entityId]
  );

  useEffect(() => {
    const controller = new AbortController();
    void fetchLinks(controller.signal);
    return () => {
      controller.abort();
    };
  }, [fetchLinks]);

  const refresh = useCallback(() => {
    const controller = new AbortController();
    void fetchLinks(controller.signal);
  }, [fetchLinks]);

  return { links, loading, error, refresh };
}
