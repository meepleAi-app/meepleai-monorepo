import { useQuery } from '@tanstack/react-query';

import { getCampaign, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';

export const gamebookCampaignKeys = {
  detail: (id: string) => ['gamebook', 'campaigns', id] as const,
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
