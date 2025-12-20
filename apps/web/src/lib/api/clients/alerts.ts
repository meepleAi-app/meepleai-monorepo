/**
 * Alert API Client (Issue #921)
 *
 * Modular client for alert management:
 * - Get active/all alerts
 * - Resolve alert by type
 */

import {
  GetAlertsResponseSchema,
  ResolveAlertResponseSchema,
  type AlertDto,
  type ResolveAlertResponse,
} from '../schemas/alerts.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateAlertsClientParams {
  httpClient: HttpClient;
}

export interface AlertsClient {
  getAlerts(activeOnly?: boolean): Promise<AlertDto[]>;
  resolveAlert(alertType: string): Promise<ResolveAlertResponse>;
}

/**
 * Create alerts client with HttpClient dependency injection
 */
export function createAlertsClient({ httpClient }: CreateAlertsClientParams): AlertsClient {
  return {
    /**
     * Get alerts (active or all)
     * @param activeOnly - If true, returns only active alerts. If false, returns 7-day history.
     */
    async getAlerts(activeOnly: boolean = true): Promise<AlertDto[]> {
      const data = await httpClient.get<AlertDto[]>(
        `/api/v1/admin/alerts?activeOnly=${activeOnly}`,
        GetAlertsResponseSchema
      );
      return data ?? [];
    },

    /**
     * Manually resolve an alert by type
     * @param alertType - The alert type to resolve (e.g., "HighErrorRate")
     */
    async resolveAlert(alertType: string): Promise<ResolveAlertResponse> {
      const data = await httpClient.post<ResolveAlertResponse>(
        `/api/v1/admin/alerts/${encodeURIComponent(alertType)}/resolve`,
        {},
        ResolveAlertResponseSchema
      );
      return data;
    },
  };
}
