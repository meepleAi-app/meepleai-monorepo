import { HttpClient } from './core/httpClient';

import type { Typology, CreateTypology, UpdateTypology } from './schemas/agent-typologies.schemas';

const api = new HttpClient();

export const agentTypologiesApi = {
  /**
   * Get all typologies
   */
  getAll: async (): Promise<Typology[]> => {
    const result = await api.get<Typology[]>('/api/v1/agent-typologies');
    return result || [];
  },

  /**
   * Get typology by ID
   */
  getById: async (id: string): Promise<Typology | null> => {
    return api.get<Typology>(`/api/v1/agent-typologies/${id}`);
  },

  /**
   * Create new typology
   */
  create: async (data: CreateTypology): Promise<{ id: string }> => {
    const result = await api.post<{ id: string }>('/api/v1/agent-typologies', data);
    if (!result) throw new Error('Failed to create typology');
    return result;
  },

  /**
   * Update existing typology
   */
  update: async (id: string, data: UpdateTypology): Promise<void> => {
    await api.put(`/api/v1/agent-typologies/${id}`, data);
  },

  /**
   * Delete typology
   */
  delete: async (id: string): Promise<void> => {
    await api.delete(`/api/v1/agent-typologies/${id}`);
  },

  /**
   * Toggle typology active status
   */
  toggle: async (id: string): Promise<void> => {
    await api.patch(`/api/v1/agent-typologies/${id}/toggle`, {});
  },
};
