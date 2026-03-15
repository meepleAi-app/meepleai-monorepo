/**
 * Admin Notifications API Client
 *
 * Client for admin manual notification dispatch.
 */

import {
  SendManualNotificationResultSchema,
  type SendManualNotificationRequest,
  type SendManualNotificationResult,
} from '../schemas/adminNotifications.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateAdminNotificationsClientParams {
  httpClient: HttpClient;
}

export function createAdminNotificationsClient({
  httpClient,
}: CreateAdminNotificationsClientParams) {
  return {
    /**
     * Send a manual notification to selected users via chosen channels.
     * POST /api/v1/admin/notifications/send
     */
    async sendManualNotification(
      request: SendManualNotificationRequest
    ): Promise<SendManualNotificationResult> {
      const response = await httpClient.post<SendManualNotificationResult>(
        '/api/v1/admin/notifications/send',
        request,
        SendManualNotificationResultSchema
      );
      return response;
    },
  };
}

export type AdminNotificationsClient = ReturnType<typeof createAdminNotificationsClient>;
