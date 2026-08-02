/**
 * Admin Cover Picker (Epic #3470 — Slice 1d-c).
 *
 * Query hook: the materialized cover source candidates + per-context assignments
 * for a shared game. Wraps `api.admin.getCoverCandidates(gameId)`.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CoverCandidates } from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from './coverEditorKeys';

export function useCoverCandidates(gameId: string): UseQueryResult<CoverCandidates | null, Error> {
  return useQuery({
    queryKey: coverEditorKeys.candidates(gameId),
    queryFn: () => api.admin.getCoverCandidates(gameId),
    enabled: !!gameId,
  });
}
