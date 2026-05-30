/**
 * Admin KB SubNav Counts hook (Issue #1655 Task 8)
 *
 * Polls GET /api/v1/admin/kb/nav-counts every 30 s and exposes
 * the badge counts for the KbSubNav tabs.
 */

import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { fetchKbNavCounts } from '@/lib/api/admin-kb-nav-counts';

export interface KbNavCounts {
  readonly queue: number | undefined;
  readonly feedback: number | undefined;
  readonly loading: boolean;
  readonly isError: boolean;
}

export function useKbNavCounts(): KbNavCounts {
  const query = useQuery({
    queryKey: ['admin', 'kb', 'nav-counts'] as const,
    queryFn: ({ signal }) => fetchKbNavCounts({ signal }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    staleTime: 15_000,
    placeholderData: keepPreviousData,
  });

  return {
    queue: query.data?.processingQueue,
    feedback: query.data?.feedback7d,
    loading: query.isLoading,
    isError: query.isError,
  };
}
