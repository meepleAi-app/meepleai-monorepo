/**
 * useUpdateKbDocMeta — Phase 3 #1737 mutation hook for PATCH /api/v1/kb-docs/{id}.
 *
 * Cache invalidation (DEC-2 aggressive — issue #1737 spec-panel):
 *   - ['kb-docs', 'user']         → KbHomeDesktop recent docs
 *   - ['kb-globale', 'search']    → search results (title affects display)
 *   - ['kb-docs', 'detail', id]   → per-doc detail cache (viewer hero)
 *
 * BE contract (PR #1732 #1687):
 *   - 200 + extended UserKbDocDto (Title, Tags, UpdatedBy added)
 *   - 404 = not accessible (anti-info-leak D-2, NOT 403)
 *   - 422 = validation envelope (FluentValidation)
 */
import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PatchKbDocMetadataRequest } from '@/lib/api/schemas/kb-docs-patch.schemas';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

export interface UpdateKbDocMetaInput {
  id: string;
  body: PatchKbDocMetadataRequest;
}

export type UseUpdateKbDocMetaResult = UseMutationResult<UserKbDocDto, Error, UpdateKbDocMetaInput>;

export function useUpdateKbDocMeta(): UseUpdateKbDocMetaResult {
  const qc = useQueryClient();
  return useMutation<UserKbDocDto, Error, UpdateKbDocMetaInput>({
    mutationFn: ({ id, body }) => api.kbDocs.patchKbDocMetadata(id, body),
    onSuccess: (_data, vars) => {
      // DEC-2 aggressive invalidation: any cached query whose key starts with
      // these prefixes will refetch on next mount/observer.
      qc.invalidateQueries({ queryKey: ['kb-docs', 'user'] });
      qc.invalidateQueries({ queryKey: ['kb-globale', 'search'] });
      qc.invalidateQueries({ queryKey: ['kb-docs', 'detail', vars.id] });
    },
  });
}
