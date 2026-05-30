/**
 * useActivityFeed — Phase 3b (#1593) cross-entity activity feed hook for the
 * `RecentActivityRail` sidebar in LibraryHub.
 *
 * Calls `GET /api/v1/activity?limit=<limit>` (BE-3 #1590), applies the adapter
 * `toActivityItem` (maps `eventType` → `ActivityKind`, derives `entityTitle`
 * with i18n fallback when `title=null`), and returns the rail's `ActivityItem[]`.
 *
 * The legacy `useActivityFeed` (PlayRecords+Badges dashboard) was renamed to
 * `useDashboardActivityFeed` (#1593 E1 prereq) to free this slot.
 */

import { useMemo } from 'react';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { ActivityItem } from '@/components/features/library/RecentActivityRail';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import type { ActivityFeedResponse } from '@/lib/api/schemas/activity.schemas';
import { toActivityItem } from '@/lib/library/activity-adapter';

export interface UseActivityFeedData {
  /** Adapted items (DTO → rail ActivityItem). */
  items: ActivityItem[];
  /** Page size returned by the BE (NOT a global total). */
  count: number;
}

/**
 * Default limit: 20 (BE-3 default).
 */
export function useActivityFeed(limit: number = 20): UseQueryResult<UseActivityFeedData> {
  const { t } = useTranslation();

  const fallbacks = useMemo(
    () => ({
      agent: t('pages.library.activityRail.fallback.agent'),
      chat: t('pages.library.activityRail.fallback.chat'),
      kbIndexed: t('pages.library.activityRail.fallback.kbIndexed'),
      play: t('pages.library.activityRail.fallback.play'),
      removed: t('pages.library.activityRail.fallback.removed'),
    }),
    [t]
  );

  return useQuery({
    queryKey: ['activity', 'feed', { limit }],
    queryFn: async () => {
      const response: ActivityFeedResponse = await api.activity.listActivity({ limit });
      return {
        items: response.items.map(dto => toActivityItem(dto, fallbacks)),
        count: response.count,
      };
    },
    staleTime: 60 * 1000,
  });
}
