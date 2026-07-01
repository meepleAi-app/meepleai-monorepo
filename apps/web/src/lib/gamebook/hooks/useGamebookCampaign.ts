import { useQuery } from '@tanstack/react-query';

import {
  getCampaign,
  getCampaignSpine,
  type GamebookCampaign,
  type GamebookCampaignSpine,
} from '@/lib/api/gamebook-campaigns';

export const gamebookCampaignKeys = {
  detail: (id: string) => ['gamebook', 'campaigns', id] as const,
  spine: (id: string) => ['gamebook', 'campaigns', id, 'spine'] as const,
};

export function useGamebookCampaign(id: string | undefined) {
  return useQuery<GamebookCampaign>({
    queryKey: id ? gamebookCampaignKeys.detail(id) : ['gamebook', 'campaigns', 'noop'],
    // `enabled: !!id` guarantees queryFn only runs when id is set.
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- gated by enabled
    queryFn: () => getCampaign(id!),
    enabled: !!id,
  });
}

/**
 * #2632 (SI-1b Phase 4): the owning GameNight "Serata" spine for a campaign, or `null` when the
 * campaign has no GameNight-attached play (standalone → the strip is not rendered).
 */
export function useGamebookCampaignSpine(id: string | undefined) {
  return useQuery<GamebookCampaignSpine | null>({
    queryKey: id ? gamebookCampaignKeys.spine(id) : ['gamebook', 'campaigns', 'noop', 'spine'],
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- gated by enabled
    queryFn: () => getCampaignSpine(id!),
    enabled: !!id,
  });
}
