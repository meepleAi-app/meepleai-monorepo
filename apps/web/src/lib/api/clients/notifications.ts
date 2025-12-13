/**
 * Notification API Client (Issue #2053)
 *
 * Modular client for user notification management:
 * - Get user notifications with filtering
 * - Get unread count for badge
 * - Mark single notification as read
 * - Mark all notifications as read
 */

import type { HttpClient } from '../core/httpClient';
import {
  GetNotificationsResponseSchema,
  GetUnreadCountResponseSchema,
  MarkAllNotificationsReadResponseSchema,
  MarkNotificationReadResponseSchema,
  type GetUnreadCountResponse,
  type MarkAllNotificationsReadResponse,
  type MarkNotificationReadResponse,
  type NotificationDto,
} from '../schemas/notifications.schemas';

export interface CreateNotificationsClientParams {
  httpClient: HttpClient;
}

export interface NotificationsClient {
  getNotifications(params?: GetNotificationsParams): Promise<NotificationDto[]>;
  getUnreadCount(): Promise<number>;
  markNotificationRead(notificationId: string): Promise<boolean>;
  markAllNotificationsRead(): Promise<number>;
}

export interface GetNotificationsParams {
  unreadOnly?: boolean;
  limit?: number;
}

/**
 * Create notifications client with HttpClient dependency injection
 */
export function createNotificationsClient({
  httpClient,
}: CreateNotificationsClientParams): NotificationsClient {
  return {
    /**
     * Get notifications for authenticated user
     * @param params - Optional filtering parameters (unreadOnly, limit)
     */
    async getNotifications(params?: GetNotificationsParams): Promise<NotificationDto[]> {
      const queryParams = new URLSearchParams();

      if (params?.unreadOnly !== undefined) {
        queryParams.append('unreadOnly', String(params.unreadOnly));
      }
      if (params?.limit !== undefined) {
        queryParams.append('limit', String(params.limit));
      }

      const queryString = queryParams.toString();
      const url = queryString ? `/api/v1/notifications?${queryString}` : '/api/v1/notifications';

      const data = await httpClient.get<NotificationDto[]>(url, GetNotificationsResponseSchema);
      return data ?? [];
    },

    /**
     * Get unread notification count (optimized for badge)
     */
    async getUnreadCount(): Promise<number> {
      const data = await httpClient.get<GetUnreadCountResponse>(
        '/api/v1/notifications/unread-count',
        GetUnreadCountResponseSchema
      );
      return data?.count ?? 0;
    },

    /**
     * Mark a single notification as read
     * @param notificationId - Notification UUID to mark as read
     * @returns true if successful, false if not found
     */
    async markNotificationRead(notificationId: string): Promise<boolean> {
      try {
        const data = await httpClient.post<MarkNotificationReadResponse>(
          `/api/v1/notifications/${notificationId}/mark-read`,
          {},
          MarkNotificationReadResponseSchema
        );
        return data?.success ?? false;
      } catch {
        // 404 or other errors
        return false;
      }
    },

    /**
     * Mark all notifications as read for authenticated user
     * @returns count of updated notifications
     */
    async markAllNotificationsRead(): Promise<number> {
      const data = await httpClient.post<MarkAllNotificationsReadResponse>(
        '/api/v1/notifications/mark-all-read',
        {},
        MarkAllNotificationsReadResponseSchema
      );
      return data?.updatedCount ?? 0;
    },
  };
}
