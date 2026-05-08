'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  listGlossary,
  bootstrapGlossary,
  upsertGlossary,
  type GamebookGlossaryEntry,
} from '@/lib/api/gamebook-glossary';

export const gamebookGlossaryKeys = {
  list: (campaignId: string) => ['gamebook', 'glossary', campaignId] as const,
};

export function useGamebookGlossary(campaignId: string) {
  return useQuery<GamebookGlossaryEntry[]>({
    queryKey: gamebookGlossaryKeys.list(campaignId),
    queryFn: () => listGlossary(campaignId),
    enabled: !!campaignId,
  });
}

export function useBootstrapGlossary(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookGlossaryEntry[], Error, void>({
    mutationFn: () => bootstrapGlossary(campaignId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: gamebookGlossaryKeys.list(campaignId) });
    },
  });
}

export interface UpsertGlossaryArgs {
  entryId: string;
  termEn: string;
  termIt: string;
}

export function useUpsertGlossary(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookGlossaryEntry, Error, UpsertGlossaryArgs>({
    mutationFn: ({ entryId, termEn, termIt }) =>
      upsertGlossary(campaignId, entryId, { termEn, termIt }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: gamebookGlossaryKeys.list(campaignId) });
    },
  });
}
