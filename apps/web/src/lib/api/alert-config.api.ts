/**
 * Alert Configuration API Client (Issue #915)
 */

import { HttpClient } from './core/httpClient';

import type {
  AlertConfiguration,
  UpdateAlertConfiguration,
  AlertConfigCategory,
} from './schemas/alert-config.schemas';

const api = new HttpClient();

export const alertConfigApi = {
  /**
   * Get all alert configurations
   */
  getAll: async (): Promise<AlertConfiguration[]> => {
    const result = await api.get<AlertConfiguration[]>('/admin/alert-configuration');
    return result || [];
  },

  /**
   * Get alert configuration by category
   */
  getByCategory: async (category: AlertConfigCategory): Promise<AlertConfiguration | null> => {
    return api.get<AlertConfiguration>(`/admin/alert-configuration/${category}`);
  },

  /**
   * Update or create alert configuration
   */
  update: async (data: UpdateAlertConfiguration): Promise<{ message: string }> => {
    const result = await api.put<{ message: string }>('/admin/alert-configuration', data);
    if (!result) throw new Error('Failed to update alert configuration');
    return result;
  },
};
