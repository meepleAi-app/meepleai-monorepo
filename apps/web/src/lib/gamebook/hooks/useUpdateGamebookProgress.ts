import { useMutation, useQueryClient } from '@tanstack/react-query';

import { updateProgress, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';

import { gamebookCampaignKeys } from './useGamebookCampaign';

/**
 * Mutation variables for `useUpdateGamebookProgress`.
 *
 * `gameBookId` is required (C2 multi-book generalization) so the backend can
 * scope progress to the right `SessionBookProgress` row. Callers must
 * supply it explicitly — see `BookPicker` for the picker UI used when a
 * game has 2+ books.
 */
export interface UpdateGamebookProgressVariables {
  gameBookId: string;
  currentParagraph: number;
}

export function useUpdateGamebookProgress(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookCampaign, Error, UpdateGamebookProgressVariables>({
    mutationFn: ({ gameBookId, currentParagraph }) =>
      updateProgress(campaignId, gameBookId, currentParagraph),
    onSuccess: data => {
      qc.setQueryData(gamebookCampaignKeys.detail(campaignId), data);
    },
  });
}
