/**
 * useEmbeddingServiceInfo / useEmbeddingServiceMetrics — #4446: GET /api/v1/admin/embedding/info and
 * /metrics, for every admin page that shows the embedding service (KB upload settings, the RAG
 * pipeline's Embedding tab and queue sidebar, the Embedding Service page).
 *
 * They used to fetch them each on their own, under the same query keys but with different types:
 * the one that fetched first filled the cache for the others. The Embedding tab read `dimensions`
 * and `totalRequests`, which the API never sent, and the queue sidebar crashed on a
 * `throughputPerSecond` it never sent either. One hook per endpoint, one key, one schema.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  EmbeddingServiceInfo,
  EmbeddingServiceMetrics,
} from '@/lib/api/schemas/admin-knowledge-base.schemas';

export const embeddingServiceInfoQueryKey = ['admin', 'embedding', 'info'] as const;
export const embeddingServiceMetricsQueryKey = ['admin', 'embedding', 'metrics'] as const;

interface PollingOptions {
  refetchInterval?: number;
}

export function useEmbeddingServiceInfo(options?: PollingOptions) {
  return useQuery<EmbeddingServiceInfo | null, Error>({
    queryKey: embeddingServiceInfoQueryKey,
    queryFn: () => api.admin.getEmbeddingInfo(),
    staleTime: 30_000,
    refetchInterval: options?.refetchInterval ?? 30_000,
  });
}

export function useEmbeddingServiceMetrics(options?: PollingOptions) {
  return useQuery<EmbeddingServiceMetrics | null, Error>({
    queryKey: embeddingServiceMetricsQueryKey,
    queryFn: () => api.admin.getEmbeddingMetrics(),
    staleTime: 15_000,
    refetchInterval: options?.refetchInterval ?? 30_000,
  });
}
