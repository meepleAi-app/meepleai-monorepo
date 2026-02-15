/**
 * Notifications History Page Tests (Issue #4425)
 *
 * Tests for full notification list page:
 * - Renders notification list from store
 * - Tab filtering (All / Unread)
 * - Type filter chips
 * - Pagination (20 items per page)
 * - "Segna tutte come lette" button
 * - Empty state
 * - Loading state
 *
 * Coverage target: 85%+
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import NotificationsPage from '../page';
import type { NotificationDto } from '@/lib/api';

// ============================================================================
// Store Mock
// ============================================================================

const mockFetchNotifications = vi.fn();
const mockMarkAllAsRead = vi.fn();

const createMockState = (overrides: Partial<{
  notifications: NotificationDto[];
  isFetching: boolean;
  error: string | null;
}> = {}) => ({
  notifications: overrides.notifications ?? [],
  unreadCount: (overrides.notifications ?? []).filter(n => !n.isRead).length,
  isFetching: overrides.isFetching ?? false,
  error: overrides.error ?? null,
  isLoading: false,
  isMarkingRead: false,
  fetchNotifications: mockFetchNotifications,
  fetchUnreadCount: vi.fn(),
  markAsRead: vi.fn(),
  markAllAsRead: mockMarkAllAsRead,
  addNotification: vi.fn(),
  clearError: vi.fn(),
  reset: vi.fn(),
});

let currentState = createMockState();

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: unknown) => unknown) => selector(currentState)),
  selectNotifications: (state: { notifications: NotificationDto[] }) => state.notifications,
  selectUnreadCount: (state: { unreadCount: number }) => state.unreadCount,
  selectUnreadNotifications: (state: { notifications: NotificationDto[] }) =>
    state.notifications.filter((n: NotificationDto) => !n.isRead),
  selectIsLoading: (state: { isLoading: boolean; isFetching: boolean }) =>
    state.isLoading || state.isFetching,
  selectError: (state: { error: string | null }) => state.error,
}));

// Mock NotificationItem to simplify DOM testing
vi.mock('@/components/notifications/NotificationItem', () => ({
  NotificationItem: ({ notification }: { notification: NotificationDto }) => (
    <div data-testid={`notification-item-${notification.id}`}>
      <span>{notification.title}</span>
      <span>{notification.message}</span>
    </div>
  ),
}));

// Mock CatalogPagination
vi.mock('@/components/catalog/CatalogPagination', () => ({
  CatalogPagination: ({
    currentPage,
    totalPages,
    onPageChange,
  }: {
    currentPage: number;
    totalPages: number;
    onPageChange: (page: number) => void;
    totalResults: number;
  }) => (
    <div data-testid="pagination">
      <span data-testid="pagination-info">Page {currentPage} of {totalPages}</span>
      {currentPage > 1 && (
        <button onClick={() => onPageChange(currentPage - 1)} data-testid="pagination-prev">
          Previous
        </button>
      )}
      {currentPage < totalPages && (
        <button onClick={() => onPageChange(currentPage + 1)} data-testid="pagination-next">
          Next
        </button>
      )}
    </div>
  ),
}));

// Mock lucide-react icons
vi.mock('lucide-react', () => ({
  Bell: ({ className }: { className?: string }) => <span className={className} data-testid="bell-icon" />,
  CheckCheck: ({ className }: { className?: string }) => <span className={className} data-testid="check-icon" />,
  Filter: ({ className }: { className?: string }) => <span className={className} data-testid="filter-icon" />,
  Loader2: ({ className }: { className?: string }) => <span className={className} data-testid="loader-icon" />,
}));

// Mock window.scrollTo
vi.stubGlobal('scrollTo', vi.fn());

// ============================================================================
// Test Data Factory
// ============================================================================

function createNotification(overrides: Partial<NotificationDto> & { id: string }): NotificationDto {
  return {
    userId: '550e8400-e29b-41d4-a716-446655440001',
    type: 'pdf_upload_completed',
    severity: 'info',
    title: `Notification ${overrides.id}`,
    message: `Message for ${overrides.id}`,
    isRead: false,
    createdAt: '2026-02-15T10:00:00Z',
    ...overrides,
  };
}

function generateNotifications(count: number, readPattern?: 'all-unread' | 'mixed'): NotificationDto[] {
  return Array.from({ length: count }, (_, i) => createNotification({
    id: `550e8400-e29b-41d4-a716-44665544${String(i).padStart(4, '0')}`,
    title: `Notification ${i + 1}`,
    message: `Message ${i + 1}`,
    isRead: readPattern === 'mixed' ? i % 2 === 0 : false,
  }));
}

// ============================================================================
// Tests
// ============================================================================

describe('NotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentState = createMockState();
  });

  describe('Rendering', () => {
    it('renders notification list from store', () => {
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440010', title: 'PDF Ready' }),
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440011', title: 'Upload Done' }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.getByText('Notifiche')).toBeInTheDocument();
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440010')).toBeInTheDocument();
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440011')).toBeInTheDocument();
    });

    it('fetches notifications on mount', () => {
      render(<NotificationsPage />);

      expect(mockFetchNotifications).toHaveBeenCalledWith({});
    });
  });

  describe('Tab Filter', () => {
    it('"Tutte" tab shows all notifications', () => {
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440020', isRead: false }),
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440021', isRead: true }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      // "Tutte" tab is active by default
      const allTab = screen.getByRole('tab', { name: /tutte/i });
      expect(allTab).toHaveAttribute('aria-selected', 'true');

      // Both notifications visible
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440020')).toBeInTheDocument();
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440021')).toBeInTheDocument();
    });

    it('"Non lette" tab shows unread only', async () => {
      const user = userEvent.setup();
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440030', isRead: false, title: 'Unread One' }),
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440031', isRead: true, title: 'Read One' }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      // Click "Non lette" tab
      const unreadTab = screen.getByRole('tab', { name: /non lette/i });
      await user.click(unreadTab);

      // Only unread visible
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440030')).toBeInTheDocument();
      expect(screen.queryByTestId('notification-item-550e8400-e29b-41d4-a716-446655440031')).not.toBeInTheDocument();
    });
  });

  describe('Type Filter', () => {
    it('type filter chips filter by notification type', async () => {
      const user = userEvent.setup();
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440040', type: 'pdf_upload_completed' }),
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440041', type: 'processing_failed' }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      // Click "Errori" chip (processing_failed type)
      const erroriChip = screen.getByText('Errori');
      await user.click(erroriChip);

      // Only processing_failed visible
      expect(screen.queryByTestId('notification-item-550e8400-e29b-41d4-a716-446655440040')).not.toBeInTheDocument();
      expect(screen.getByTestId('notification-item-550e8400-e29b-41d4-a716-446655440041')).toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('shows pagination for more than 20 items', () => {
      const notifications = generateNotifications(25);
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.getByTestId('pagination')).toBeInTheDocument();
      expect(screen.getByTestId('pagination-info')).toHaveTextContent('Page 1 of 2');
    });

    it('navigation works across pages', async () => {
      const user = userEvent.setup();
      const notifications = generateNotifications(25);
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      // Page 1: first 20 items visible
      expect(screen.getByTestId('pagination-info')).toHaveTextContent('Page 1 of 2');

      // Click next
      await user.click(screen.getByTestId('pagination-next'));

      // Page 2: remaining 5 items
      expect(screen.getByTestId('pagination-info')).toHaveTextContent('Page 2 of 2');
    });

    it('does not show pagination for 20 or fewer items', () => {
      const notifications = generateNotifications(15);
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.queryByTestId('pagination')).not.toBeInTheDocument();
    });
  });

  describe('Mark All As Read', () => {
    it('"Segna tutte come lette" button calls markAllAsRead', async () => {
      const user = userEvent.setup();
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440050', isRead: false }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      const markAllButton = screen.getByRole('button', { name: /segna tutte come lette/i });
      await user.click(markAllButton);

      expect(mockMarkAllAsRead).toHaveBeenCalled();
    });

    it('hides mark all button when no unread notifications', () => {
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440060', isRead: true }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.queryByRole('button', { name: /segna tutte come lette/i })).not.toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('shows empty state when no notifications', () => {
      currentState = createMockState({ notifications: [] });

      render(<NotificationsPage />);

      expect(screen.getByText('Nessuna notifica')).toBeInTheDocument();
    });

    it('shows empty state message for unread tab with no unread', async () => {
      const user = userEvent.setup();
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440070', isRead: true }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      const unreadTab = screen.getByRole('tab', { name: /non lette/i });
      await user.click(unreadTab);

      // The empty state message in the content area (not header)
      const emptyMessages = screen.getAllByText('Nessuna notifica non letta');
      // Header shows "Nessuna notifica non letta" + empty state shows it too
      expect(emptyMessages.length).toBeGreaterThanOrEqual(1);
    });
  });

  describe('Loading State', () => {
    it('shows loading spinner while fetching', () => {
      currentState = createMockState({ isFetching: true });

      render(<NotificationsPage />);

      expect(screen.getByTestId('loader-icon')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('shows error message on fetch failure', () => {
      currentState = createMockState({ error: 'Failed to fetch notifications' });

      render(<NotificationsPage />);

      expect(screen.getByText('Failed to fetch notifications')).toBeInTheDocument();
    });
  });

  describe('Unread Count Display', () => {
    it('shows unread count in header', () => {
      const notifications = generateNotifications(3, 'all-unread');
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.getByText('3 non lette')).toBeInTheDocument();
    });

    it('shows "Nessuna notifica non letta" when all read', () => {
      const notifications = [
        createNotification({ id: '550e8400-e29b-41d4-a716-446655440080', isRead: true }),
      ];
      currentState = createMockState({ notifications });

      render(<NotificationsPage />);

      expect(screen.getByText('Nessuna notifica non letta')).toBeInTheDocument();
    });
  });
});
