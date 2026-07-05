'use client';

/**
 * useResumeGamebookSitting — SI-4 (#2635, step 3).
 *
 * Opens a NEW live Session for a gamebook campaign on its already-InProgress (or Published)
 * game-night — the "resume the sitting" action on the play page. Wraps the Attach path
 * (POST /game-nights/{id}/gamebook-sessions). On success it invalidates the night-live + diary
 * reads (single source of truth, no optimistic flip) and the campaign spine (its live/session
 * counts change). A max-1-live 409 reaches the view discriminably via `isMaxLiveBlockedError`.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import {
  gameNightSessionClient,
  type StartSessionResponse,
} from '@/lib/api/clients/gameNightSessionClient';
import { gameNightLiveKeys } from '@/lib/game-nights/hooks/useGameNightLive';
import { nightLiveDiaryKeys } from '@/lib/game-nights/hooks/useNightLiveDiary';

import { gamebookCampaignKeys } from './useGamebookCampaign';

export interface ResumeGamebookSittingVars {
  readonly campaignId: string;
}

export function useResumeGamebookSitting(
  gameNightId: string
): UseMutationResult<StartSessionResponse, Error, ResumeGamebookSittingVars> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ campaignId }: ResumeGamebookSittingVars) =>
      gameNightSessionClient.attachGamebookCampaign(gameNightId, campaignId),
    onSuccess: (_data, { campaignId }) => {
      void queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.detail(gameNightId) });
      void queryClient.invalidateQueries({ queryKey: nightLiveDiaryKeys.detail(gameNightId) });
      // The spine's hasLiveSession/session counts change after a new sitting opens.
      void queryClient.invalidateQueries({ queryKey: gamebookCampaignKeys.spine(campaignId) });
    },
    retry: false,
  });
}
