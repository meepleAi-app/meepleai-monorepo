/**
 * NotificationCenter Component Tests (Issue #4947)
 *
 * Tests for the side drawer showing notifications in two sections:
 * - "NUOVE" (unread) with teal styling
 * - "PRECEDENTI" (read)
 * - Empty state, error state, KB-ready CTA
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { NotificationCenter } from '../NotificationCenter';
import { useNotificationStore } from '@/store/notification/store';
import type { NotificationDto } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    onClick,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    onClick?: () => void;
    [key: string]: unknown;
  }) => (
    <a href={href} onClick={onClick} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn(),
  selectNotifications: vi.fn((state: { notifications: NotificationDto[] }) => state.notifications),
}));

// ============================================================================
// Test Data
// ============================================================================

const makeNotification = (overrides: Partial<NotificationDto>): NotificationDto => ({
  id: 'notif-1',
  userId: 'user-1',
  type: 'pdf_upload_completed',
  severity: 'info',
  title: 'PDF uploaded',
  message: 'Your PDF has been processed',
  link: null,
  metadata: null,
  isRead: false,
  createdAt: '2024-01-01T10:00:00.000Z',
  readAt: null,
  ...overrides,
});

const mockUnread = makeNotification({
  id: 'notif-unread-1',
  type: 'pdf_upload_completed',
  severity: 'info',
  title: 'PDF uploaded',
  isRead: false,
});

const mockRead = makeNotification({
  id: 'notif-read-1',
  type: 'new_comment',
  severity: 'info',
  title: 'New comment',
  message: 'Someone commented on your game',
  isRead: true,
  readAt: '2024-01-01T11:00:00.000Z',
});

const mockKbReady = makeNotification({
  id: 'notif-kb-1',
  type: 'processing_job_completed',
  severity: 'success',
  title: 'KB ready',
  message: 'Your knowledge base is ready for agent creation',
  link: '/library/games/game-abc/agent',
  isRead: false,
});

const mockKbReadyNoLink = makeNotification({
  id: 'notif-kb-2',
  type: 'processing_job_completed',
  severity: 'success',
  title: 'KB ready (no link)',
  isRead: false,
  link: null,
});

// ============================================================================
// Test Helpers
// ============================================================================

function setupStore(
  notifications: NotificationDto[] = [],
  options: { isFetching?: boolean; error?: string | null } = {}
) {
  const mockFetchNotifications = vi.fn().mockResolvedValue(undefined);
  const mockMarkAllAsRead = vi.fn().mockResolvedValue(undefined);
  const mockMarkAsRead = vi.fn().mockResolvedValue(undefined);

  vi.mocked(useNotificationStore).mockImplementation(selector => {
    const state = {
      notifications,
      isFetching: options.isFetching ?? false,
      error: options.error ?? null,
      fetchNotifications: mockFetchNotifications,
      markAllAsRead: mockMarkAllAsRead,
      markAsRead: mockMarkAsRead,
    };
    return typeof selector === 'function' ? selector(state) : state;
  });

  return { mockFetchNotifications, mockMarkAllAsRead, mockMarkAsRead };
}

// ============================================================================
// Tests
// ============================================================================

describe('NotificationCenter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // Closed state
  // --------------------------------------------------------------------------

  describe('when closed (open=false)', () => {
    it('should not render sheet content when closed', () => {
      setupStore([]);
      render(<NotificationCenter open={false} onOpenChange={vi.fn()} />);

      // Radix UI Sheet does not render content when closed
      expect(screen.queryByText('notificationCenter.title')).not.toBeInTheDocument();
    });

    it('should not fetch notifications when closed', async () => {
      const { mockFetchNotifications } = setupStore([]);
      render(<NotificationCenter open={false} onOpenChange={vi.fn()} />);

      // Wait a tick to ensure no unexpected calls
      await new Promise(resolve => setTimeout(resolve, 10));
      expect(mockFetchNotifications).not.toHaveBeenCalled();
    });
  });

  // --------------------------------------------------------------------------
  // Open state — structure
  // --------------------------------------------------------------------------

  describe('when open (open=true)', () => {
    it('should render the sheet title', () => {
      setupStore([]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.title')).toBeInTheDocument();
    });

    it('should fetch notifications on open', async () => {
      const { mockFetchNotifications } = setupStore([]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      await waitFor(() => {
        expect(mockFetchNotifications).toHaveBeenCalledWith({ limit: 30 });
      });
    });

    it('should re-fetch when opened after being closed', async () => {
      const { mockFetchNotifications } = setupStore([]);
      const { rerender } = render(<NotificationCenter open={false} onOpenChange={vi.fn()} />);

      expect(mockFetchNotifications).not.toHaveBeenCalled();

      rerender(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      await waitFor(() => {
        expect(mockFetchNotifications).toHaveBeenCalledTimes(1);
      });
    });

    // -------------------------------------------------------------------------
    // Empty state
    // -------------------------------------------------------------------------

    it('should show empty state when there are no notifications', () => {
      setupStore([]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.empty')).toBeInTheDocument();
      expect(screen.getByText('notificationCenter.emptySubtext')).toBeInTheDocument();
    });

    it('should not show "View all" link when there are no notifications', () => {
      setupStore([]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.queryByTestId('notification-center-view-all')).not.toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Loading state
    // -------------------------------------------------------------------------

    it('should show loading text while fetching with no existing notifications', () => {
      setupStore([], { isFetching: true });
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.loading')).toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Error state
    // -------------------------------------------------------------------------

    it('should show error message when there is an error', () => {
      setupStore([], { error: 'Network error' });
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Unread (NUOVE) section
    // -------------------------------------------------------------------------

    it('should show NUOVE section heading for unread notifications', () => {
      setupStore([mockUnread]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.newSection')).toBeInTheDocument();
    });

    it('should render unread notification title in NUOVE section', () => {
      setupStore([mockUnread]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('PDF uploaded')).toBeInTheDocument();
    });

    it('should not show NUOVE section when there are no unread notifications', () => {
      setupStore([mockRead]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.queryByText('notificationCenter.newSection')).not.toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Read (PRECEDENTI) section
    // -------------------------------------------------------------------------

    it('should show PRECEDENTI section heading for read notifications', () => {
      setupStore([mockRead]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.previousSection')).toBeInTheDocument();
    });

    it('should render read notification title in PRECEDENTI section', () => {
      setupStore([mockRead]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('New comment')).toBeInTheDocument();
    });

    it('should show both NUOVE and PRECEDENTI sections with mixed notifications', () => {
      setupStore([mockUnread, mockRead]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.newSection')).toBeInTheDocument();
      expect(screen.getByText('notificationCenter.previousSection')).toBeInTheDocument();
      expect(screen.getByText('PDF uploaded')).toBeInTheDocument();
      expect(screen.getByText('New comment')).toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Mark all as read
    // -------------------------------------------------------------------------

    it('should show "Mark all read" button when there are unread notifications', () => {
      setupStore([mockUnread]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      const button = screen.getByRole('button', {
        name: 'notificationCenter.markAllRead',
      });
      expect(button).toBeInTheDocument();
    });

    it('should not show "Mark all read" button when all notifications are read', () => {
      setupStore([mockRead]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(
        screen.queryByRole('button', { name: 'notificationCenter.markAllRead' })
      ).not.toBeInTheDocument();
    });

    it('should call markAllAsRead when button is clicked', async () => {
      const user = userEvent.setup();
      const { mockMarkAllAsRead } = setupStore([mockUnread]);

      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      const button = screen.getByRole('button', { name: 'notificationCenter.markAllRead' });
      await user.click(button);

      expect(mockMarkAllAsRead).toHaveBeenCalledTimes(1);
    });

    // -------------------------------------------------------------------------
    // KB-ready CTA (processing_job_completed)
    // -------------------------------------------------------------------------

    it('should render KB-ready CTA for processing_job_completed notification with link', () => {
      setupStore([mockKbReady]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      const cta = screen.getByTestId(`kb-ready-cta-${mockKbReady.id}`);
      expect(cta).toBeInTheDocument();
      expect(cta).toHaveAttribute('href', '/library/games/game-abc/agent');
    });

    it('should show KB-ready CTA text', () => {
      setupStore([mockKbReady]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.getByText('notificationCenter.kbReady.cta')).toBeInTheDocument();
    });

    it('should not render KB-ready CTA when link is null', () => {
      setupStore([mockKbReadyNoLink]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(
        screen.queryByTestId(`kb-ready-cta-${mockKbReadyNoLink.id}`)
      ).not.toBeInTheDocument();
    });

    it('should not render KB-ready CTA for non-KB notification types', () => {
      setupStore([mockUnread]); // pdf_upload_completed type
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      expect(screen.queryByTestId(`kb-ready-cta-${mockUnread.id}`)).not.toBeInTheDocument();
    });

    // -------------------------------------------------------------------------
    // Footer — View all link
    // -------------------------------------------------------------------------

    it('should show "View all" link when notifications exist', () => {
      setupStore([mockUnread]);
      render(<NotificationCenter open={true} onOpenChange={vi.fn()} />);

      const viewAll = screen.getByTestId('notification-center-view-all');
      expect(viewAll).toBeInTheDocument();
      expect(viewAll).toHaveAttribute('href', '/notifications');
    });

    it('should close the drawer when "View all" is clicked', async () => {
      const user = userEvent.setup();
      const mockOnOpenChange = vi.fn();
      setupStore([mockUnread]);

      render(<NotificationCenter open={true} onOpenChange={mockOnOpenChange} />);

      const viewAll = screen.getByTestId('notification-center-view-all');
      await user.click(viewAll);

      expect(mockOnOpenChange).toHaveBeenCalledWith(false);
    });
  });
});
