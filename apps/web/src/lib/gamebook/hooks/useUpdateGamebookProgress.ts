import { useMutation, useQueryClient } from '@tanstack/react-query';

import { updateProgress, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';

import { gamebookCampaignKeys } from './useGamebookCampaign';

export function useUpdateGamebookProgress(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookCampaign, Error, number>({
    mutationFn: currentParagraph => updateProgress(campaignId, currentParagraph),
    onSuccess: data => {
      qc.setQueryData(gamebookCampaignKeys.detail(campaignId), data);
    },
  });
}
