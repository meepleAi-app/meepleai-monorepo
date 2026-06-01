/**
 * React Query hook for the authenticated user's achievements.
 *
 * Mirrors the wire format of `Gamification.AchievementDto` (BE: Issue #3922).
 * Endpoint: `GET /api/v1/achievements` (already exposed by `AchievementEndpoints.cs`).
 *
 * Consumers:
 *  - `apps/web/src/components/profile/AchievementsGrid.tsx` — the full grid UI
 *  - `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx`
 *    — derives `achievementCount` for the player overview header (#1542)
 *
 * @see Issue #1542 (player achievements list + FE hook, #1478 follow-up)
 */

import { useQuery } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';

/**
 * Achievement row as returned by `GET /api/v1/achievements`.
 *
 * Type mirrors `Api.BoundedContexts.Gamification.Application.DTOs.AchievementDto`
 * (BE). Kept as an interface — no Zod parsing — to match the pattern already in
 * `AchievementsGrid.tsx`; tighten with a Zod schema if/when the contract diverges.
 */
export interface AchievementDto {
  id: string;
  code: string;
  name: string;
  description: string;
  iconUrl: string;
  points: number;
  rarity: string;
  category: string;
  threshold: number;
  /** Progress toward unlock in [0, 100]. Null when not tracking. */
  progress: number | null;
  isUnlocked: boolean;
  /** ISO date-time string. Null when not unlocked. */
  unlockedAt: string | null;
}

export const achievementsKeys = {
  all: ['achievements'] as const,
};

/** Fetch all achievements with the user's unlock status. */
export function useAchievements() {
  return useQuery<AchievementDto[]>({
    queryKey: achievementsKeys.all,
    queryFn: async () => {
      const res = await apiClient.get<AchievementDto[]>('/api/v1/achievements');
      return res ?? [];
    },
    staleTime: 5 * 60 * 1000,
  });
}
