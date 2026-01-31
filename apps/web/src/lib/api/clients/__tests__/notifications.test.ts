/**
 * Notifications Client Tests - Issue #2340
 * Coverage target: notifications.ts (111 lines)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createNotificationsClient } from '../notifications';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as any;

describe('NotificationsClient - Issue #2340', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getNotifications', () => {
    it('should fetch notifications without params', async () => {
      const mockNotifications = [{ id: '1', title: 'Test', isRead: false }];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockNotifications);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.getNotifications();

      expect(result).toEqual(mockNotifications);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/notifications', expect.any(Object));
    });

    it('should filter unread notifications when unreadOnly=true', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      await client.getNotifications({ unreadOnly: true });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/notifications?unreadOnly=true',
        expect.any(Object)
      );
    });

    it('should apply limit param', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      await client.getNotifications({ limit: 50 });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/notifications?limit=50',
        expect.any(Object)
      );
    });

    it('should combine multiple params', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      await client.getNotifications({ unreadOnly: true, limit: 20 });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('unreadOnly=true'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('limit=20'),
        expect.any(Object)
      );
    });

    it('should return empty array when get returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.getNotifications();

      expect(result).toEqual([]);
    });
  });

  describe('getUnreadCount', () => {
    it('should fetch unread count', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ count: 5 });

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.getUnreadCount();

      expect(result).toBe(5);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/notifications/unread-count',
        expect.any(Object)
      );
    });

    it('should return 0 when response is null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.getUnreadCount();

      expect(result).toBe(0);
    });
  });

  describe('markNotificationRead', () => {
    it('should mark notification as read and return true', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({ success: true });

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.markNotificationRead('notif-123');

      expect(result).toBe(true);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/notifications/notif-123/mark-read',
        {},
        expect.any(Object)
      );
    });

    it('should return false when post throws error', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(new Error('404'));

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.markNotificationRead('invalid-id');

      expect(result).toBe(false);
    });

    it('should return false when response.success is false', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({ success: false });

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.markNotificationRead('notif-123');

      expect(result).toBe(false);
    });
  });

  describe('markAllNotificationsRead', () => {
    it('should mark all as read and return updated count', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue({ updatedCount: 10 });

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.markAllNotificationsRead();

      expect(result).toBe(10);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/notifications/mark-all-read',
        {},
        expect.any(Object)
      );
    });

    it('should return 0 when response is null', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(null);

      const client = createNotificationsClient({ httpClient: mockHttpClient });
      const result = await client.markAllNotificationsRead();

      expect(result).toBe(0);
    });
  });
});
