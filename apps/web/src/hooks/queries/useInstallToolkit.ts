/**
 * useInstallToolkit — TanStack Query mutation hook for the toolkit
 * install action (Wave 3 Phase 2, Issue #805 / PR #732 §5.3.5).
 *
 * Backend contract: `POST /api/v1/toolkits/{toolkitId}/install` returns
 * `{ installCount, hasInstalled }`. 404 when the toolkit is missing or
 * yanked. Idempotent (PR #732 §5.3.5 Nygard) — repeated calls return 200
 * with the current state, NEVER 409.
 *
 * Side-effects: backend invalidates the discover popularity cache; this
 * hook also invalidates the toolkit-detail query so `viewerContext.has
 * Installed` flips to true on the next render.
 *
 * Schema reality v1 carryover (Gate B): `installCount` is always 0 until
 * Phase 4 wires the ToolkitInstallation entity. The `hasInstalled` flag
 * still flips to `true` on success so the FE button-state machine can
 * render the "Installed" pill.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  InstallToolkitResponseSchema,
  type InstallToolkitResponse,
} from '@/lib/api/schemas/toolkit-marketplace.schemas';

import { toolkitDetailKeys } from './useToolkitDetail';

export interface InstallToolkitVariables {
  readonly toolkitId: string;
}

/**
 * Install (or re-install, idempotently) a marketplace toolkit.
 *
 * @example
 * const installToolkit = useInstallToolkit();
 * await installToolkit.mutateAsync({ toolkitId: 'xxx' });
 */
export function useInstallToolkit(): UseMutationResult<
  InstallToolkitResponse,
  Error,
  InstallToolkitVariables
> {
  const queryClient = useQueryClient();

  return useMutation<InstallToolkitResponse, Error, InstallToolkitVariables>({
    mutationFn: async ({ toolkitId }) => {
      // apiClient.post returns the parsed body; schema validates wire shape.
      return await apiClient.post<InstallToolkitResponse>(
        `/api/v1/toolkits/${toolkitId}/install`,
        // Empty body — install endpoint takes no payload.
        undefined,
        InstallToolkitResponseSchema
      );
    },
    onSuccess: async (_data, variables) => {
      // Refresh the detail envelope so viewerContext.hasInstalled flips on
      // the next render. The backend invalidates the per-viewer cache too;
      // this client-side invalidation just shortens the round-trip.
      await queryClient.invalidateQueries({
        queryKey: toolkitDetailKeys.byId(variables.toolkitId),
      });
    },
  });
}
