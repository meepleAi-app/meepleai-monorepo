import { HttpClient } from './core/httpClient';

import type {
  AlertChannel,
  AlertChannelType,
  TestAlertChannelConnectionResult,
  UpsertAlertChannelRequest,
} from './schemas/alert-channels.schemas';

const api = new HttpClient();

/**
 * Issue #1840 SP5 F4-C7 — Alert channels (email + slack) configuration.
 *
 * Backend routes: `apps/api/src/Api/Routing/AlertChannelsEndpoints.cs`.
 * Auth: admin-only (`.RequireAdminSession()` enforced per-endpoint).
 */
export const alertChannelsApi = {
  /**
   * List all configured alert channels for the Canali drawer.
   * Returns empty array if backend is unreachable.
   */
  getAll: async (): Promise<AlertChannel[]> => {
    const result = await api.get<AlertChannel[]>('/api/v1/admin/alert-channels');
    return result || [];
  },

  /**
   * Create or update a channel configuration. `rowVersion` is required when
   * updating an existing channel — the backend returns 409 ConflictException
   * if the row was modified concurrently.
   */
  upsert: async (
    type: AlertChannelType,
    body: UpsertAlertChannelRequest
  ): Promise<AlertChannel> => {
    const result = await api.put<AlertChannel>(`/api/v1/admin/alert-channels/${type}`, body);
    if (!result) throw new Error(`Failed to upsert alert channel: ${type}`);
    return result;
  },

  /**
   * Probe the channel transport (Slack: webhook POST · Email: SMTP sanity-check).
   * The backend persists `lastTestedAt` / `lastTestStatus` on the channel row
   * so subsequent GETs reflect the latest probe.
   */
  testConnection: async (type: AlertChannelType): Promise<TestAlertChannelConnectionResult> => {
    const result = await api.post<TestAlertChannelConnectionResult>(
      `/api/v1/admin/alert-channels/${type}/test-connection`,
      {}
    );
    if (!result) throw new Error(`Test connection returned no payload: ${type}`);
    return result;
  },
};
