/**
 * NotificationPanel Component Tests (Issue #4412)
 *
 * Tests for dropdown panel UX polish:
 * - Preview limit: max 5 notifications displayed
 * - Footer: "Vedi tutte le notifiche" with /notifications link
 * - Empty/loading/error states
 * - Mark all read button
 *
 * Coverage target: ≥85%
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { NotificationPanel } from '../NotificationPanel';
import type { NotificationDto } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

const mockFetchNotifications = vi.fn();
const mockMarkAllAsRead = vi.fn();

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock next/navigation (needed by NotificationItem)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock PdfStatusBadge
vi.mock('@/components/pdf', () => ({
  PdfStatusBadge: ({ state }: { state: string }) => (
    <span data-testid="pdf-status-badge">{state}</span>
  ),
}));

// Store state container
let storeState: Record<string, unknown> = {};

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: Record<string, unknown>) => unknown) => {
    return typeof selector === 'function' ? selector(storeState) : storeState;
  }),
  selectNotifications: (state: Record<string, unknown>) => state.notifications,
  selectIsLoading: (state: Record<string, unknown>) => state.isLoading || state.isFetching,
  selectUnreadCount: (state: Record<string, unknown>) => state.unreadCount,
  selectError: (state: Record<string, unknown>) => state.error,
}));

// ============================================================================
// Helpers
// ============================================================================

function createNotification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: crypto.randomUUID(),
    userId: '00000000-0000-0000-0000-000000000001',
    type: 'pdf_upload_completed',
    severity: 'success',
    title: 'PDF Ready',
    message: 'Your document is ready',
    link: null,
    metadata: null,
    isRead: false,
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  };
}

function setupStore(overrides: Partial<typeof storeState> = {}) {
  storeState = {
    notifications: [],
    unreadCount: 0,
    isLoading: false,
    isFetching: false,
    isMarkingRead: false,
    error: null,
    fetchNotifications: mockFetchNotifications,
    markAllAsRead: mockMarkAllAsRead,
    markAsRead: vi.fn(),
    fetchUnreadCount: vi.fn(),
    addNotification: vi.fn(),
    clearError: vi.fn(),
    reset: vi.fn(),
    ...overrides,
  };
}

// ============================================================================
// Tests
// ============================================================================

describe('NotificationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupStore();
  });

  // ---- Preview Limit ----

  it('should fetch notifications with limit 5 on mount', () => {
    setupStore();

    render(<NotificationPanel />);

    expect(mockFetchNotifications).toHaveBeenCalledWith({ limit: 5 });
  });

  it('should render at most 5 notifications in preview', () => {
    const notifications = Array.from({ length: 5 }, (_, i) =>
      createNotification({
        id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`,
        title: `Notification ${i + 1}`,
      })
    );
    setupStore({ notifications });

    render(<NotificationPanel />);

    for (let i = 1; i <= 5; i++) {
      expect(screen.getByText(`Notification ${i}`)).toBeInTheDocument();
    }
  });

  // ---- Footer "Vedi tutte" ----

  it('should render footer with "Vedi tutte le notifiche" link to /notifications', () => {
    const notifications = [createNotification()];
    setupStore({ notifications });

    render(<NotificationPanel />);

    const link = screen.getByTestId('view-all-notifications-link');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/notifications');
    expect(link).toHaveTextContent('Vedi tutte le notifiche');
  });

  it('should not render footer when there are no notifications', () => {
    setupStore({ notifications: [] });

    render(<NotificationPanel />);

    expect(screen.queryByTestId('view-all-notifications-link')).not.toBeInTheDocument();
  });

  it('should not render footer when there is an error', () => {
    setupStore({ notifications: [createNotification()], error: 'Network error' });

    render(<NotificationPanel />);

    expect(screen.queryByTestId('view-all-notifications-link')).not.toBeInTheDocument();
  });

  // ---- States ----

  it('should show loading state when fetching with no existing notifications', () => {
    setupStore({ isFetching: true, notifications: [] });

    render(<NotificationPanel />);

    expect(screen.getByText('Loading notifications...')).toBeInTheDocument();
  });

  it('should show empty state when no notifications', () => {
    setupStore({ notifications: [], isFetching: false });

    render(<NotificationPanel />);

    expect(screen.getByText('No notifications yet')).toBeInTheDocument();
  });

  it('should show error state', () => {
    setupStore({ error: 'Failed to load' });

    render(<NotificationPanel />);

    expect(screen.getByText('Failed to load')).toBeInTheDocument();
  });

  // ---- Mark All Read ----

  it('should show "Mark all read" button when there are unread notifications', async () => {
    const user = userEvent.setup();
    const notifications = [createNotification({ isRead: false })];
    setupStore({ notifications });

    render(<NotificationPanel />);

    const markAllButton = screen.getByRole('button', { name: /mark all/i });
    expect(markAllButton).toBeInTheDocument();

    await user.click(markAllButton);
    expect(mockMarkAllAsRead).toHaveBeenCalledTimes(1);
  });

  it('should not show "Mark all read" when all notifications are read', () => {
    const notifications = [createNotification({ isRead: true })];
    setupStore({ notifications });

    render(<NotificationPanel />);

    expect(screen.queryByRole('button', { name: /mark all/i })).not.toBeInTheDocument();
  });
});
