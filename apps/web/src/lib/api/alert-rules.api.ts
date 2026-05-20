import { HttpClient } from './core/httpClient';

import type {
  AlertRule,
  CreateAlertRule,
  UpdateAlertRule,
  AlertTemplate,
} from './schemas/alert-rules.schemas';

const api = new HttpClient();

export const alertRulesApi = {
  getAll: async (): Promise<AlertRule[]> => {
    const result = await api.get<AlertRule[]>('/api/v1/admin/alert-rules');
    return result || [];
  },

  getById: async (id: string): Promise<AlertRule | null> => {
    return api.get<AlertRule>(`/api/v1/admin/alert-rules/${id}`);
  },

  create: async (data: CreateAlertRule): Promise<{ id: string }> => {
    const result = await api.post<{ id: string }>('/api/v1/admin/alert-rules', data);
    if (!result) throw new Error('Failed to create alert rule');
    return result;
  },

  update: async (id: string, data: UpdateAlertRule): Promise<void> => {
    await api.put(`/api/v1/admin/alert-rules/${id}`, data);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/api/v1/admin/alert-rules/${id}`);
  },

  toggle: async (id: string): Promise<void> => {
    await api.patch(`/api/v1/admin/alert-rules/${id}/toggle`, {});
  },

  getTemplates: async (): Promise<AlertTemplate[]> => {
    const result = await api.get<AlertTemplate[]>('/api/v1/admin/alert-templates');
    return result || [];
  },

  testAlert: async (alertType: string, channel: string): Promise<{ success: boolean }> => {
    const result = await api.post<{ success: boolean }>('/api/v1/admin/alert-test', {
      alertType,
      channel,
    });
    return result || { success: false };
  },
};
