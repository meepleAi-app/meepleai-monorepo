/**
 * NotificationBell Component Tests (Issue #2053)
 *
 * Tests for bell icon with badge:
 * - Badge visibility based on unread count
 * - Badge formatting (0 = hidden, 1-9 = number, 10+ = "9+")
 * - Accessibility (ARIA labels)
 * - Auto-fetch on mount
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotificationBell } from '../NotificationBell';
import { useNotificationStore } from '@/store/notification/store';

// Mock store with all selectors
vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn(),
  selectUnreadCount: vi.fn(state => state.unreadCount),
  selectNotifications: vi.fn(state => state.notifications),
  selectIsLoading: vi.fn(state => state.isLoading || state.isFetching),
  selectUnreadNotifications: vi.fn(state => state.notifications?.filter(n => !n.isRead) ?? []),
  selectError: vi.fn(state => state.error),
}));

describe('NotificationBell', () => {
  const mockFetchUnreadCount = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render bell icon without badge when unread count is 0', () => {
    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = { unreadCount: 0, fetchUnreadCount: mockFetchUnreadCount };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    const button = screen.getByRole('button', { name: /notifications/i });
    expect(button).toBeInTheDocument();

    // Badge should not exist
    const badge = screen.queryByText(/\d+/);
    expect(badge).not.toBeInTheDocument();
  });

  it('should render badge with count when unread notifications exist', () => {
    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = { unreadCount: 5, fetchUnreadCount: mockFetchUnreadCount };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    const badge = screen.getByText('5');
    expect(badge).toBeInTheDocument();

    const button = screen.getByRole('button', { name: /notifications \(5 unread\)/i });
    expect(button).toBeInTheDocument();
  });

  it('should display "9+" for 10 or more unread notifications', () => {
    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = { unreadCount: 15, fetchUnreadCount: mockFetchUnreadCount };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    const badge = screen.getByText('9+');
    expect(badge).toBeInTheDocument();
  });

  it('should fetch unread count on mount', async () => {
    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = { unreadCount: 0, fetchUnreadCount: mockFetchUnreadCount };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    await waitFor(() => {
      expect(mockFetchUnreadCount).toHaveBeenCalledTimes(1);
    });
  });

  it('should be keyboard accessible', async () => {
    const user = userEvent.setup();

    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = {
        unreadCount: 3,
        notifications: [],
        isLoading: false,
        isFetching: false,
        error: null,
        fetchUnreadCount: mockFetchUnreadCount,
        fetchNotifications: vi.fn(),
        markAsRead: vi.fn(),
        markAllAsRead: vi.fn(),
        addNotification: vi.fn(),
        clearError: vi.fn(),
        reset: vi.fn(),
      };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    const button = screen.getByRole('button', { name: /notifications/i });

    // Tab to button
    await user.tab();
    expect(button).toHaveFocus();

    // Press Enter to open (Shadcn DropdownMenu handles this)
    await user.keyboard('{Enter}');
    // Panel opens (tested by Shadcn/ui, we just verify button responds to keyboard)
  });

  it('should have correct ARIA attributes', () => {
    vi.mocked(useNotificationStore).mockImplementation(selector => {
      const state = { unreadCount: 7, fetchUnreadCount: mockFetchUnreadCount };
      return typeof selector === 'function' ? selector(state) : state;
    });

    render(<NotificationBell />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Notifications (7 unread)');
  });
});
