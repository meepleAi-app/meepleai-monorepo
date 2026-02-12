import { apiClient } from './client';
import type { UserPermissions, PermissionCheckResponse } from '@/types/permissions';

export async function getUserPermissions(): Promise<UserPermissions> {
  const response = await apiClient.get<UserPermissions>('/api/v1/permissions/me');
  return response.data;
}

export async function checkPermission(feature: string, state?: string): Promise<PermissionCheckResponse> {
  const params = new URLSearchParams({ feature });
  if (state) params.append('state', state);
  const response = await apiClient.get<PermissionCheckResponse>(`/api/v1/permissions/check?${params}`);
  return response.data;
}
