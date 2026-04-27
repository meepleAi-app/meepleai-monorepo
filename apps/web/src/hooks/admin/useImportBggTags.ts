/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 18).
 *
 * Mutation hook: bulk-import BGG tags for a shared game from the paste-importer
 * dialog. Wraps `api.admin.importBggTags(sharedGameId, tags)` and surfaces both
 * `inserted` and `skipped` counts via the success toast so duplicates are
 * visible to operators (Task 17 contract — see `BggImportResult`).
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type {
  BggTagInput,
  ImportBggTagsResponse,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export interface UseImportBggTagsVariables {
  sharedGameId: string;
  tags: BggTagInput[];
}

function formatSuccessMessage(inserted: number, skipped: number): string {
  if (skipped <= 0) {
    return `Imported ${inserted} BGG tag(s)`;
  }
  const noun = skipped === 1 ? 'duplicate' : 'duplicates';
  return `Imported ${inserted} BGG tag(s) (${skipped} skipped as ${noun})`;
}

export function useImportBggTags(): UseMutationResult<
  ImportBggTagsResponse,
  Error,
  UseImportBggTagsVariables
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ sharedGameId, tags }: UseImportBggTagsVariables) =>
      api.admin.importBggTags(sharedGameId, tags),
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.golden.byGame(variables.sharedGameId),
      });

      toast.success(formatSuccessMessage(data.inserted, data.skipped));
    },
    onError: error => {
      toast.error(`Failed to import BGG tags: ${error.message}`);
    },
  });
}
