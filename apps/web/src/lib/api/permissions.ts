import type { UserPermissions, PermissionCheckResponse } from '@/types/permissions';

import { apiClient } from './client';

export async function getUserPermissions(): Promise<UserPermissions> {
  const response = await apiClient.get<UserPermissions>('/api/v1/permissions/me');
  if (!response) throw new Error('Failed to fetch user permissions');
  return response;
}

export async function checkPermission(feature: string, state?: string): Promise<PermissionCheckResponse> {
  const params = new URLSearchParams({ feature });
  if (state) params.append('state', state);
  const response = await apiClient.get<PermissionCheckResponse>(`/api/v1/permissions/check?${params}`);
  if (!response) throw new Error('Failed to check permission');
  return response;
}
