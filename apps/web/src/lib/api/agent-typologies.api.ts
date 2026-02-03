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

  /**
   * Get my proposals (Editor only)
   */
  getMyProposals: async (): Promise<Typology[]> => {
    const result = await api.get<Typology[]>('/api/v1/agent-typologies/my-proposals');
    return result || [];
  },

  /**
   * Propose new typology (Editor - creates as Draft)
   */
  propose: async (data: CreateTypology): Promise<{ id: string }> => {
    const result = await api.post<{ id: string }>('/api/v1/agent-typologies/propose', data);
    if (!result) throw new Error('Failed to propose typology');
    return result;
  },

  /**
   * Test typology in sandbox (Editor - Draft only)
   */
  test: async (id: string, testQuery: string): Promise<{ success: boolean; response: string; confidenceScore: number }> => {
    const result = await api.post<{ success: boolean; response: string; confidenceScore: number }>(
      `/api/v1/agent-typologies/${id}/test`,
      { testQuery }
    );
    if (!result) throw new Error('Failed to test typology');
    return result;
  },

  /**
   * Submit typology for approval (Editor - Draft → PendingReview)
   * Temporary: Uses update endpoint until dedicated submit endpoint is implemented
   */
  submitForApproval: async (id: string, typology: Typology): Promise<void> => {
    // Backend domain has SubmitForApproval() method that changes Draft → Pending
    // Until dedicated endpoint exists, use update to trigger status change
    await api.put(`/api/v1/agent-typologies/${id}`, {
      name: typology.name,
      description: typology.description,
      basePrompt: typology.basePrompt,
      defaultStrategyName: typology.defaultStrategyName,
      defaultStrategyParameters: typology.defaultStrategyParameters ?? undefined,
    });
  },
};
