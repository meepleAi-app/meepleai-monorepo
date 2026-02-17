'use client';

import { useQuery } from '@tanstack/react-query';

import { useApiClient } from '@/lib/api/context';

/**
 * Hook for fetching PDF storage health metrics.
 * Auto-refreshes every 30 seconds.
 */
export function useStorageHealth() {
  const apiClient = useApiClient();

  return useQuery({
    queryKey: ['admin', 'pdf-storage-health'],
    queryFn: () => apiClient.pdf.getStorageHealth(),
    refetchInterval: 30_000,
  });
}
