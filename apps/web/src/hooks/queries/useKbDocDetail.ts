/**
 * useKbDocDetail — TanStack Query hook for the /kb/[id] hero metadata
 * (Wave 3 Phase 3, Issue #805 / PR #732 §6.3.1).
 *
 * Backend contract: `GET /api/v1/kb-docs/{id}`.
 *  - 200: full KbDocDetail when processingStatus === 'ready'.
 *  - 404: doc not found (hook surfaces as null via apiClient).
 *  - 403: private doc, non-owner, non-admin (apiClient throws).
 *  - 423 Locked: doc exists but is in queued/processing/failed state.
 *    The hook intercepts this status and returns a structured
 *    `KbDocLockedEnvelope` rather than throwing — so the FE can render
 *    "Documento in elaborazione" without a try/catch.
 *
 * Cache: backend caches 1h via HybridCache; FE staleTime mirrors that.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import { ApiError } from '@/lib/api/core/errors';
import {
  KbDocDetailSchema,
  type KbDocDetail,
  type KbDocEnvelope,
  type KbProcessingStatus,
} from '@/lib/api/schemas/kb-chunks.schemas';

export const KB_DOC_DETAIL_STALE_TIME_MS = 60 * 60 * 1000; // 1h, mirrors backend cache.

export const kbDocDetailKeys = {
  all: ['kb', 'docs', 'detail'] as const,
  byId: (docId: string) => [...kbDocDetailKeys.all, docId] as const,
};

export interface UseKbDocDetailOptions {
  readonly docId: string | null | undefined;
  readonly enabled?: boolean;
}

interface ApiErrorWithStatus extends Error {
  statusCode?: number;
}

function isLockedError(err: unknown): err is ApiErrorWithStatus {
  return err instanceof ApiError && err.statusCode === 423;
}

/**
 * Best-effort extractor for the processingStatus from a 423 response payload.
 * Returns 'processing' as a sentinel default when the message can't be
 * parsed — the FE will render the "in elaborazione" state regardless.
 */
function extractProcessingStatus(err: ApiErrorWithStatus): KbProcessingStatus {
  const msg = err.message ?? '';
  if (msg.includes("'failed'")) return 'failed';
  if (msg.includes("'queued'")) return 'queued';
  if (msg.includes("'processing'")) return 'processing';
  return 'processing';
}

/**
 * Fetch the spec-conformant doc detail envelope. Result is a discriminated
 * union: `{ status: 'ready', doc }` or `{ status: 'locked', ... }`.
 *
 * @example
 * const { data } = useKbDocDetail({ docId: 'xxx' });
 * if (data?.status === 'ready') { showHero(data.doc); }
 * else if (data?.status === 'locked') { showInProgressBanner(); }
 */
export function useKbDocDetail(
  options: UseKbDocDetailOptions
): UseQueryResult<KbDocEnvelope | null, Error> {
  const { docId, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;

  return useQuery<KbDocEnvelope | null, Error>({
    queryKey: isValid ? kbDocDetailKeys.byId(docId) : kbDocDetailKeys.all,
    queryFn: async () => {
      if (!isValid) return null;
      try {
        const doc = await apiClient.get<KbDocDetail>(`/api/v1/kb-docs/${docId}`, KbDocDetailSchema);
        if (!doc) return null;
        return { status: 'ready', doc } satisfies KbDocEnvelope;
      } catch (err) {
        if (isLockedError(err)) {
          return {
            status: 'locked',
            processingStatus: extractProcessingStatus(err),
            // The 423 response from the backend does not carry the partial
            // DTO in the v1 wire shape (the spec leaves this implicit). The
            // hook surfaces null and the FE renders an in-progress banner
            // without metadata. A follow-up may attach the partial DTO to
            // the 423 body.
            doc: null,
          } satisfies KbDocEnvelope;
        }
        throw err;
      }
    },
    enabled: enabled && isValid,
    staleTime: KB_DOC_DETAIL_STALE_TIME_MS,
    // 423 is a terminal state for this query — never retry.
    retry: (failureCount, error) => {
      if (isLockedError(error)) return false;
      return failureCount < 2;
    },
  });
}
