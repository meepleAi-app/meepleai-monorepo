import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  closeCampaign,
  type GamebookCampaign,
  type GamebookCampaignOutcomeValue,
} from '@/lib/api/gamebook-campaigns';

import { gamebookCampaignKeys } from './useGamebookCampaign';

/**
 * SI-8 (#2639): terminally close a gamebook campaign (Completa/Abbandona) from
 * the play-evening-end 3-way selector. "Archivia" (resumable) does NOT use this
 * hook — it leaves the campaign open.
 *
 * On success, primes the campaign detail cache with the returned (now-closed)
 * DTO and invalidates the broad campaign family + this campaign's spine so the
 * resume picker and the Serata strip reflect the terminal state (mirrors the
 * SI-3 cross-surface invalidation contract).
 */
export function useCloseGamebookCampaign(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookCampaign, Error, GamebookCampaignOutcomeValue>({
    mutationFn: outcome => closeCampaign(campaignId, outcome),
    onSuccess: data => {
      qc.setQueryData(gamebookCampaignKeys.detail(campaignId), data);
      qc.invalidateQueries({ queryKey: gamebookCampaignKeys.all });
      qc.invalidateQueries({ queryKey: gamebookCampaignKeys.spine(campaignId) });
    },
  });
}
