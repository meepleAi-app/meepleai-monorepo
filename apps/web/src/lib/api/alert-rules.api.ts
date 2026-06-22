import { HttpClient } from './core/httpClient';

import type {
  AlertRule,
  CreateAlertRule,
  UpdateAlertRule,
  AlertTemplate,
  TestAlertRuleMode,
  TestAlertRuleResult,
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

  /**
   * Issue #1840 SP5 F4-C7 — Per-rule TestAlert.
   * Defaults to `dryRun` (no external send, only emits AlertFiredEvent for the
   * activity feed). Pass `mode: 'live'` to actually dispatch to channels — the
   * UI MUST gate this behind a confirm dialog.
   */
  testRule: async (
    id: string,
    mode: TestAlertRuleMode = 'dryRun'
  ): Promise<TestAlertRuleResult> => {
    const result = await api.post<TestAlertRuleResult>(
      `/api/v1/admin/alert-rules/${id}/test?mode=${mode}`,
      {}
    );
    if (!result) throw new Error('Test alert rule returned no payload');
    return result;
  },
};
