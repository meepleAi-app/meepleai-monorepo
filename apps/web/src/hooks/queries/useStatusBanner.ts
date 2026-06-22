/**
 * useStatusBanner — public Status Banner polling hook (Issue #1089).
 *
 * Backend contract: `GET /api/v1/status-banner`
 *  - 200: PublicStatusBannerResponse
 *  - 204: no active banner → hook returns `null`
 *
 * Polling: 60s background refetch keeps the banner near-real-time without
 * SSE. Refetches on window focus so users coming back to the tab see the
 * latest incident state immediately.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  PublicStatusBannerResponseSchema,
  type PublicStatusBannerResponse,
} from '@/lib/api/schemas/status-banner.schemas';

export const STATUS_BANNER_STALE_TIME_MS = 60_000;
export const STATUS_BANNER_REFETCH_INTERVAL_MS = 60_000;

export const statusBannerKeys = {
  all: ['status-banner'] as const,
};

/**
 * Fetches the active public status banner. Returns `null` when the backend
 * responds with 204 No Content (no banner active).
 *
 * The HttpClient already converts 204 to `null` for `get()`.
 */
export function useStatusBanner(): UseQueryResult<PublicStatusBannerResponse | null, Error> {
  return useQuery<PublicStatusBannerResponse | null, Error>({
    queryKey: statusBannerKeys.all,
    queryFn: async () => {
      const result = await apiClient.get<PublicStatusBannerResponse>(
        '/api/v1/status-banner',
        PublicStatusBannerResponseSchema
      );
      return result ?? null;
    },
    staleTime: STATUS_BANNER_STALE_TIME_MS,
    refetchInterval: STATUS_BANNER_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
    retry: 1,
  });
}
