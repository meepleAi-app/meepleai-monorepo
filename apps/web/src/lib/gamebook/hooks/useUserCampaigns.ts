import { useQuery } from '@tanstack/react-query';

import { listMyCampaigns, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';

import { gamebookCampaignKeys } from './useGamebookCampaign';

export const userCampaignsKeys = {
  list: (gameId?: string) => [...gamebookCampaignKeys.detail('list'), gameId ?? 'all'] as const,
};

const STALE_AFTER_DAYS = 90;
const MS_PER_DAY = 1000 * 60 * 60 * 24;

export interface UserCampaignWithStale extends GamebookCampaign {
  /** True quando lastReadAt è più vecchio di 90 giorni rispetto a `now`. */
  isStale: boolean;
}

/**
 * Deriva il flag `isStale` confrontando `lastReadAt` con `now`.
 * Esposto per testing — il default usa `Date.now()`.
 */
export function deriveIsStale(
  lastReadAt: string,
  now: Date = new Date(),
  thresholdDays: number = STALE_AFTER_DAYS
): boolean {
  const last = new Date(lastReadAt).getTime();
  if (Number.isNaN(last)) return false;
  const ageMs = now.getTime() - last;
  return ageMs >= thresholdDays * MS_PER_DAY;
}

export function useUserCampaigns(gameId?: string) {
  return useQuery<UserCampaignWithStale[]>({
    queryKey: userCampaignsKeys.list(gameId),
    queryFn: async () => {
      const list = await listMyCampaigns(gameId);
      const now = new Date();
      return list.map(c => ({ ...c, isStale: deriveIsStale(c.lastReadAt, now) }));
    },
  });
}
