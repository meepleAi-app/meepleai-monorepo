'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import * as api from '../lib/catalog-seed-api';

import type { BulkEnqueueRequest, EnqueueCatalogSeedRequest } from '../lib/catalog-seed-types';

/**
 * Issue #1903 M8.1 — React Query bindings for the admin catalog seed pipeline.
 *
 * Query key convention: `['catalog-seeds', ...]`. Mutations invalidate the broad
 * `['catalog-seeds']` namespace so any list filter / detail view consuming the
 * data is refreshed in a single broadcast.
 */
const CATALOG_SEEDS_KEY = ['catalog-seeds'] as const;
const CATALOG_SEED_DETAIL_KEY = ['catalog-seed-detail'] as const;

// ─── Queries ─────────────────────────────────────────────────────────────────

export function useCatalogSeeds(params: api.ListSeedsParams = {}) {
  return useQuery({
    queryKey: [...CATALOG_SEEDS_KEY, params],
    queryFn: () => api.listSeeds(params),
  });
}

export function useCatalogSeed(id: string | null) {
  return useQuery({
    queryKey: [...CATALOG_SEED_DETAIL_KEY, id],
    queryFn: () => (id ? api.getSeedById(id) : Promise.resolve(null)),
    enabled: id !== null,
  });
}

// ─── Mutations ───────────────────────────────────────────────────────────────

export function useEnqueueSeed() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: EnqueueCatalogSeedRequest) => api.enqueueSeed(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: CATALOG_SEEDS_KEY }),
  });
}

export function useBulkEnqueueSeeds() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: BulkEnqueueRequest) => api.bulkEnqueueSeeds(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: CATALOG_SEEDS_KEY }),
  });
}

export function useApproveSeed() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.approveSeed(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: CATALOG_SEEDS_KEY }),
  });
}

export function useRejectSeed() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => api.rejectSeed(id, reason),
    onSuccess: () => qc.invalidateQueries({ queryKey: CATALOG_SEEDS_KEY }),
  });
}

export function useWikidataSearch() {
  return useMutation({
    mutationFn: (searchTerm: string) => api.wikidataSearch(searchTerm),
  });
}
