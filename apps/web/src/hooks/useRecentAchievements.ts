/**
 * useRecentAchievements Hook - Issue #3924
 *
 * React Query hook for fetching recent achievements and next progress
 *
 * @see Issue #3922 - Achievement System & Badge Engine (Backend)
 * @see Issue #3924 - Frontend: Achievements Widget Component
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { Achievement, NextAchievementProgress } from '@/components/dashboard/AchievementsWidget';
import { fetchRecentAchievements } from '@/lib/api/achievements';

/** 5 minutes in milliseconds - matches backend cache TTL */
const FIVE_MINUTES_MS = 5 * 60 * 1000;

export interface RecentAchievementsData {
  achievements: Achievement[];
  nextProgress: NextAchievementProgress | null;
}

/**
 * Fetch and cache recent achievements with next progress
 *
 * Features:
 * - 5-minute stale time (matches backend cache TTL)
 * - Auto-refresh every 5 minutes
 * - Retry on failure (2 attempts)
 */
export function useRecentAchievements(): UseQueryResult<RecentAchievementsData, Error> {
  return useQuery({
    queryKey: ['achievements', 'recent'],
    queryFn: fetchRecentAchievements,
    staleTime: FIVE_MINUTES_MS,
    refetchInterval: FIVE_MINUTES_MS,
    retry: 2,
    refetchOnWindowFocus: false,
  });
}
