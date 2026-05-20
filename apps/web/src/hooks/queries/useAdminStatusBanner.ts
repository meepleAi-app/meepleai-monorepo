/**
 * useAdminStatusBanner — admin Status Banner read + update hooks (Issue #1089).
 *
 * Backend contract:
 *  - GET  /api/v1/admin/status-banner → 200 AdminStatusBannerResponse
 *  - PUT  /api/v1/admin/status-banner → 200 AdminStatusBannerResponse
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';
import {
  AdminStatusBannerResponseSchema,
  type AdminStatusBannerResponse,
  type UpdateStatusBannerCommand,
} from '@/lib/api/schemas/status-banner.schemas';

import { statusBannerKeys } from './useStatusBanner';

export const adminStatusBannerKeys = {
  all: ['admin', 'status-banner'] as const,
};

export function useAdminStatusBanner() {
  return useQuery<AdminStatusBannerResponse, Error>({
    queryKey: adminStatusBannerKeys.all,
    queryFn: async () => {
      const result = await apiClient.get<AdminStatusBannerResponse>(
        '/api/v1/admin/status-banner',
        AdminStatusBannerResponseSchema
      );
      if (!result) {
        throw new Error('Admin status banner endpoint returned empty response');
      }
      return result;
    },
    staleTime: 30_000,
  });
}

export function useUpdateStatusBanner() {
  const queryClient = useQueryClient();

  return useMutation<AdminStatusBannerResponse, Error, UpdateStatusBannerCommand>({
    mutationFn: async command => {
      return apiClient.put<AdminStatusBannerResponse>(
        '/api/v1/admin/status-banner',
        command,
        AdminStatusBannerResponseSchema
      );
    },
    onSuccess: () => {
      // Both the public read model and the admin read model can change after
      // an update — invalidate both query keys.
      queryClient.invalidateQueries({ queryKey: adminStatusBannerKeys.all });
      queryClient.invalidateQueries({ queryKey: statusBannerKeys.all });
    },
  });
}
