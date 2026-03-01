'use client';

/**
 * useEntityLinks — shared hook
 *
 * Fetches EntityLinks for a given source entity.
 * Used by RelatedEntitiesSection (C5) and EntityRelationshipGraph (C7).
 *
 * Issue #5129 — Epic C
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { api } from '@/lib/api';

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
  const abortRef = useRef<AbortController | null>(null);

  const fetchLinks = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setLoading(true);
    setError(null);
    try {
      const data = await api.entityLinks.getEntityLinks(entityType, entityId);
      if (!controller.signal.aborted) {
        setLinks(data);
      }
    } catch (err) {
      if (controller.signal.aborted) return;
      setError(err instanceof Error ? err.message : 'Failed to load links');
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
      }
    }
  }, [entityType, entityId]);

  useEffect(() => {
    void fetchLinks();
    return () => {
      abortRef.current?.abort();
    };
  }, [fetchLinks]);

  const refresh = useCallback(() => {
    void fetchLinks();
  }, [fetchLinks]);

  return { links, loading, error, refresh };
}
