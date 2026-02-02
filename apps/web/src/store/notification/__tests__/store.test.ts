/**
 * Notification Store Tests (Issue #2053)
 *
 * Tests for notification Zustand store:
 * - fetchNotifications
 * - fetchUnreadCount
 * - markAsRead
 * - markAllAsRead
 * - addNotification
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useNotificationStore } from '../store';
import { api } from '@/lib/api';
import type { NotificationDto } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    notifications: {
      getNotifications: vi.fn(),
      getUnreadCount: vi.fn(),
      markNotificationRead: vi.fn(),
      markAllNotificationsRead: vi.fn(),
    },
  },
}));

// Test fixtures
const createMockNotification = (overrides?: Partial<NotificationDto>): NotificationDto => ({
  id: 'notif-1',
  userId: 'user-1',
  type: 'pdf_upload_completed',
  severity: 'success',
  title: 'Upload Complete',
  message: 'Your PDF has been processed successfully',
  link: '/pdf/doc-1',
  metadata: null,
  isRead: false,
  createdAt: new Date().toISOString(),
  readAt: null,
  ...overrides,
});

describe('useNotificationStore', () => {
  beforeEach(() => {
    // Reset store before each test
    const { result } = renderHook(() => useNotificationStore());
    act(() => {
      result.current.reset();
    });

    // Clear mock calls
    vi.clearAllMocks();
  });

  describe('fetchNotifications', () => {
    it('should fetch and store notifications', async () => {
      const mockNotifications = [
        createMockNotification({ id: 'notif-1', isRead: false }),
        createMockNotification({ id: 'notif-2', isRead: true }),
      ];

      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.notifications).toEqual(mockNotifications);
      expect(result.current.unreadCount).toBe(1); // Only notif-1 is unread
      expect(result.current.isFetching).toBe(false);
    });

    it('should handle fetch errors gracefully', async () => {
      vi.mocked(api.notifications.getNotifications).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.error).toBe('Network error');
      expect(result.current.notifications).toEqual([]);
      expect(result.current.isFetching).toBe(false);
    });

    it('should support unreadOnly filtering', async () => {
      vi.mocked(api.notifications.getNotifications).mockResolvedValue([]);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications({ unreadOnly: true, limit: 10 });
      });

      expect(api.notifications.getNotifications).toHaveBeenCalledWith({
        unreadOnly: true,
        limit: 10,
      });
    });
  });

  describe('fetchUnreadCount', () => {
    it('should fetch and update unread count', async () => {
      vi.mocked(api.notifications.getUnreadCount).mockResolvedValue(5);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchUnreadCount();
      });

      expect(result.current.unreadCount).toBe(5);
    });

    it('should silently fail on error (non-critical)', async () => {
      vi.mocked(api.notifications.getUnreadCount).mockRejectedValue(new Error('API error'));

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchUnreadCount();
      });

      // Should not throw, count stays at 0, no error set (silent fail)
      expect(result.current.unreadCount).toBe(0);
      expect(result.current.error).toBeNull(); // Silent fail, no error state
    });
  });

  describe('markAsRead', () => {
    it('should mark notification as read and decrement count', async () => {
      const mockNotification = createMockNotification({ id: 'notif-1', isRead: false });
      vi.mocked(api.notifications.getNotifications).mockResolvedValue([mockNotification]);
      vi.mocked(api.notifications.markNotificationRead).mockResolvedValue(true);

      const { result } = renderHook(() => useNotificationStore());

      // Setup: Fetch notifications first
      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.unreadCount).toBe(1);

      // Mark as read
      await act(async () => {
        await result.current.markAsRead('notif-1');
      });

      expect(api.notifications.markNotificationRead).toHaveBeenCalledWith('notif-1');
      expect(result.current.notifications[0]?.isRead).toBe(true);
      expect(result.current.notifications[0]?.readAt).toBeTruthy();
      expect(result.current.unreadCount).toBe(0);
    });

    it('should handle API failure gracefully', async () => {
      vi.mocked(api.notifications.markNotificationRead).mockResolvedValue(false);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.markAsRead('notif-1');
      });

      expect(result.current.error).toBeTruthy();
    });

    it('should not decrement count below zero', async () => {
      const { result } = renderHook(() => useNotificationStore());

      act(() => {
        result.current.notifications = [];
        result.current.unreadCount = 0;
      });

      vi.mocked(api.notifications.markNotificationRead).mockResolvedValue(true);

      await act(async () => {
        await result.current.markAsRead('notif-1');
      });

      expect(result.current.unreadCount).toBe(0); // Should not go negative
    });
  });

  describe('markAllAsRead', () => {
    it('should mark all notifications as read', async () => {
      const mockNotifications = [
        createMockNotification({ id: 'notif-1', isRead: false }),
        createMockNotification({ id: 'notif-2', isRead: false }),
        createMockNotification({ id: 'notif-3', isRead: true }),
      ];

      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);
      vi.mocked(api.notifications.markAllNotificationsRead).mockResolvedValue(2);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.unreadCount).toBe(2);

      await act(async () => {
        await result.current.markAllAsRead();
      });

      expect(result.current.notifications.every(n => n.isRead)).toBe(true);
      expect(result.current.unreadCount).toBe(0);
    });
  });

  describe('addNotification', () => {
    it('should add new notification to list', () => {
      const { result } = renderHook(() => useNotificationStore());
      const newNotification = createMockNotification({ id: 'notif-new' });

      act(() => {
        result.current.addNotification(newNotification);
      });

      expect(result.current.notifications).toHaveLength(1);
      expect(result.current.notifications[0]).toEqual(newNotification);
      expect(result.current.unreadCount).toBe(1);
    });

    it('should prevent duplicate notifications', () => {
      const { result } = renderHook(() => useNotificationStore());
      const notification = createMockNotification({ id: 'notif-1' });

      act(() => {
        result.current.addNotification(notification);
        result.current.addNotification(notification); // Duplicate
      });

      expect(result.current.notifications).toHaveLength(1);
    });

    it('should add to start of list (newest first)', () => {
      const { result } = renderHook(() => useNotificationStore());
      const notif1 = createMockNotification({ id: 'notif-1' });
      const notif2 = createMockNotification({ id: 'notif-2' });

      act(() => {
        result.current.addNotification(notif1);
        result.current.addNotification(notif2);
      });

      expect(result.current.notifications[0]?.id).toBe('notif-2'); // Newest first
      expect(result.current.notifications[1]?.id).toBe('notif-1');
    });
  });

  describe('clearError', () => {
    it('should clear error state', async () => {
      vi.mocked(api.notifications.getNotifications).mockRejectedValue(new Error('Test error'));

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.error).toBeTruthy();

      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBeNull();
    });
  });

  describe('reset', () => {
    it('should reset store to initial state', async () => {
      const mockNotifications = [createMockNotification()];
      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.notifications).toHaveLength(1);

      act(() => {
        result.current.reset();
      });

      expect(result.current.notifications).toEqual([]);
      expect(result.current.unreadCount).toBe(0);
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });

  describe('markAsRead edge cases', () => {
    it('should handle API rejection with error message', async () => {
      vi.mocked(api.notifications.markNotificationRead).mockRejectedValue(
        new Error('Server unavailable')
      );

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.markAsRead('notif-1');
      });

      expect(result.current.error).toBe('Server unavailable');
      expect(result.current.isMarkingRead).toBe(false);
    });

    it('should handle non-Error rejection', async () => {
      vi.mocked(api.notifications.markNotificationRead).mockRejectedValue('API Failure');

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.markAsRead('notif-1');
      });

      expect(result.current.error).toBe('Failed to mark notification as read');
    });
  });

  describe('markAllAsRead error handling', () => {
    it('should handle API rejection with error message', async () => {
      vi.mocked(api.notifications.markAllNotificationsRead).mockRejectedValue(
        new Error('Bulk operation failed')
      );

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.markAllAsRead();
      });

      expect(result.current.error).toBe('Bulk operation failed');
      expect(result.current.isMarkingRead).toBe(false);
    });

    it('should handle non-Error rejection', async () => {
      vi.mocked(api.notifications.markAllNotificationsRead).mockRejectedValue({ status: 500 });

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.markAllAsRead();
      });

      expect(result.current.error).toBe('Failed to mark all notifications as read');
    });
  });

  describe('fetchNotifications error handling', () => {
    it('should handle non-Error rejection', async () => {
      vi.mocked(api.notifications.getNotifications).mockRejectedValue('String error');

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      expect(result.current.error).toBe('Failed to fetch notifications');
    });
  });

  describe('addNotification with read status', () => {
    it('should not increment unread count for already-read notification', () => {
      const { result } = renderHook(() => useNotificationStore());
      const readNotification = createMockNotification({
        id: 'notif-read',
        isRead: true,
        readAt: new Date().toISOString()
      });

      act(() => {
        result.current.addNotification(readNotification);
      });

      expect(result.current.notifications).toHaveLength(1);
      expect(result.current.unreadCount).toBe(0);
    });
  });
});

// Import selectors for testing
import {
  selectNotifications,
  selectUnreadCount,
  selectUnreadNotifications,
  selectIsLoading,
  selectError,
} from '../store';

describe('Notification Store Selectors', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useNotificationStore());
    act(() => {
      result.current.reset();
    });
    vi.clearAllMocks();
  });

  describe('selectNotifications', () => {
    it('should return notifications array', async () => {
      const mockNotifications = [
        createMockNotification({ id: 'notif-1' }),
        createMockNotification({ id: 'notif-2' }),
      ];
      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      const state = useNotificationStore.getState();
      expect(selectNotifications(state)).toEqual(mockNotifications);
    });

    it('should return empty array initially', () => {
      const state = useNotificationStore.getState();
      expect(selectNotifications(state)).toEqual([]);
    });
  });

  describe('selectUnreadCount', () => {
    it('should return unread count', async () => {
      const mockNotifications = [
        createMockNotification({ id: 'notif-1', isRead: false }),
        createMockNotification({ id: 'notif-2', isRead: false }),
        createMockNotification({ id: 'notif-3', isRead: true }),
      ];
      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      const state = useNotificationStore.getState();
      expect(selectUnreadCount(state)).toBe(2);
    });
  });

  describe('selectUnreadNotifications', () => {
    it('should return only unread notifications', async () => {
      const mockNotifications = [
        createMockNotification({ id: 'notif-1', isRead: false }),
        createMockNotification({ id: 'notif-2', isRead: true }),
        createMockNotification({ id: 'notif-3', isRead: false }),
      ];
      vi.mocked(api.notifications.getNotifications).mockResolvedValue(mockNotifications);

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      const state = useNotificationStore.getState();
      const unread = selectUnreadNotifications(state);
      expect(unread).toHaveLength(2);
      expect(unread.every(n => !n.isRead)).toBe(true);
    });
  });

  describe('selectIsLoading', () => {
    it('should return true when isFetching is true', async () => {
      vi.mocked(api.notifications.getNotifications).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      const { result } = renderHook(() => useNotificationStore());

      act(() => {
        result.current.fetchNotifications();
      });

      const state = useNotificationStore.getState();
      expect(selectIsLoading(state)).toBe(true);
    });

    it('should return false when not loading', () => {
      const state = useNotificationStore.getState();
      expect(selectIsLoading(state)).toBe(false);
    });
  });

  describe('selectError', () => {
    it('should return error string when error exists', async () => {
      vi.mocked(api.notifications.getNotifications).mockRejectedValue(
        new Error('Test error')
      );

      const { result } = renderHook(() => useNotificationStore());

      await act(async () => {
        await result.current.fetchNotifications();
      });

      const state = useNotificationStore.getState();
      expect(selectError(state)).toBe('Test error');
    });

    it('should return null when no error', () => {
      const state = useNotificationStore.getState();
      expect(selectError(state)).toBeNull();
    });
  });
});
