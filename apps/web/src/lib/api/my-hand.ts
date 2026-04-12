import { apiClient } from './client';

export interface HandSlotDto {
  slotType: string;
  entityId: string | null;
  entityType: string | null;
  entityLabel: string | null;
  entityImageUrl: string | null;
  pinnedAt: string | null;
}

export interface UpdateHandSlotPayload {
  entityId: string;
  entityType: string;
  entityLabel: string | null;
  entityImageUrl: string | null;
}

export async function getMyHand(): Promise<HandSlotDto[]> {
  const result = await apiClient.get<HandSlotDto[]>('/users/me/hand');
  return result ?? [];
}

export async function updateHandSlot(
  slotType: string,
  payload: UpdateHandSlotPayload
): Promise<HandSlotDto> {
  return apiClient.put<HandSlotDto>(`/users/me/hand/${slotType}`, payload);
}

export async function clearHandSlot(slotType: string): Promise<void> {
  await apiClient.delete(`/users/me/hand/${slotType}`);
}
