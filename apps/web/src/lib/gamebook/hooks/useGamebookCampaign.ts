import { useQuery } from '@tanstack/react-query';

import { getCampaign, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';

export const gamebookCampaignKeys = {
  detail: (id: string) => ['gamebook', 'campaigns', id] as const,
};

export function useGamebookCampaign(id: string | undefined) {
  return useQuery<GamebookCampaign>({
    queryKey: id ? gamebookCampaignKeys.detail(id) : ['gamebook', 'campaigns', 'noop'],
    queryFn: () => getCampaign(id!),
    enabled: !!id,
  });
}
