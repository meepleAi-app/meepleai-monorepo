'use client';

import { useQuery } from '@tanstack/react-query';

import { listHistory, type TranslatedParagraph } from '@/lib/api/gamebook-history';

export const gamebookHistoryKeys = {
  list: (campaignId: string) => ['gamebook', 'history', campaignId] as const,
};

export function useGamebookHistory(campaignId: string) {
  return useQuery<TranslatedParagraph[]>({
    queryKey: gamebookHistoryKeys.list(campaignId),
    queryFn: () => listHistory(campaignId),
    enabled: !!campaignId,
  });
}
