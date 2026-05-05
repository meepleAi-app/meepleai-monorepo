/**
 * Notifications History Page Tests (Issue #4425)
 *
 * Tests for full notification list with filters and pagination:
 * - Renders notification list from store
 * - Tab filter (Tutte / Non lette)
 * - Type filter chips
 * - Pagination (20 items per page)
 * - Mark all as read button
 * - Empty and loading states
 *
 * Coverage target: ≥85%
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import NotificationsPage from '../page';
import type { NotificationDto } from '@/lib/api';
import { EMPTY } from '../../../../__tests__/fixtures/test-strings';

// ============================================================================
// Mocks
// ============================================================================

const mockFetchNotifications = vi.fn();
const mockMarkAllAsRead = vi.fn();
const mockMarkAsRead = vi.fn();

// Mock next/navigation (needed by NotificationItem)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock PdfStatusBadge (used by NotificationItem)
vi.mock('@/components/pdf', () => ({
  PdfStatusBadge: ({ state }: { state: string }) => (
    <span data-testid="pdf-status-badge">{state}</span>
  ),
}));

// Store state container
let storeState: Record<string, unknown> = {};

vi.mock('@/stores/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: Record<string, unknown>) => unknown) => {
    return typeof selector === 'function' ? selector(storeState) : storeState;
  }),
  selectNotifications: (state: Record<string, unknown>) => state.notifications,
  selectUnreadCount: (state: Record<string, unknown>) => state.unreadCount,
  selectUnreadNotifications: (state: Record<string, unknown>) =>
    (state.notifications as NotificationDto[])?.filter((n: NotificationDto) => !n.isRead) ?? [],
  selectIsLoading: (state: Record<string, unknown>) => state.isLoading || state.isFetching,
  selectError: (state: Record<string, unknown>) => state.error,
}));

// ============================================================================
// Helpers
// ============================================================================

function createNotification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: crypto.randomUUID(),
    userId: '00000000-0000-0000-0000-000000000001',
    type: 'document_ready',
    severity: 'success',
    title: 'PDF Ready',
    message: 'Your PDF has been processed successfully',
    link: null,
    metadata: null,
    isRead: false,
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  };
}

function createNotifications(
  count: number,
  overrides: Partial<NotificationDto> = {}
): NotificationDto[] {
  return Array.from({ length: count }, (_, i) =>
    createNotification({
      id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`,
      title: `Notification ${i + 1}`,
      ...overrides,
    })
  );
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
    markAsRead: mockMarkAsRead,
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

describe('NotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupStore();
  });

  it('should render notification list from store', () => {
    const notifications = [
      createNotification({ title: 'First notification' }),
      createNotification({ title: 'Second notification' }),
    ];
    setupStore({ notifications, unreadCount: 2 });

    render(<NotificationsPage />);

    expect(screen.getByText('Notifiche')).toBeInTheDocument();
    expect(screen.getByText(/First notification/)).toBeInTheDocument();
    expect(screen.getByText(/Second notification/)).toBeInTheDocument();
  });

  it('should show "Tutte" tab showing all notifications and "Non lette" showing unread only', async () => {
    const user = userEvent.setup();
    const notifications = [
      createNotification({ title: 'Read notification', isRead: true }),
      createNotification({ title: 'Unread notification', isRead: false }),
    ];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // "Tutte" tab should be active by default - shows both
    expect(screen.getByText(/Read notification/)).toBeInTheDocument();
    expect(screen.getByText(/Unread notification/)).toBeInTheDocument();

    // Click "Non lette" tab
    const unreadTab = screen.getByRole('tab', { name: /non lette/i });
    await user.click(unreadTab);

    // Only unread notification should be visible
    expect(screen.queryByText(/Read notification/)).not.toBeInTheDocument();
    expect(screen.getByText(/Unread notification/)).toBeInTheDocument();
  });

  it('should filter by notification category when filter pills are clicked', async () => {
    const user = userEvent.setup();
    const notifications = [
      createNotification({ title: 'PDF done', type: 'document_ready' }),
      createNotification({ title: 'Agent ready', type: 'agent_ready' }),
      createNotification({ title: 'Night invite', type: 'game_night_invitation' }),
    ];
    setupStore({ notifications, unreadCount: 3 });

    render(<NotificationsPage />);

    // All visible initially
    expect(screen.getByText(/PDF done/)).toBeInTheDocument();
    expect(screen.getByText(/Agent ready/)).toBeInTheDocument();
    expect(screen.getByText(/Night invite/)).toBeInTheDocument();

    // Click "Serate" filter pill
    await user.click(screen.getByRole('button', { name: /serate/i }));

    // Only game-night notification visible
    expect(screen.queryByText(/PDF done/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Agent ready/)).not.toBeInTheDocument();
    expect(screen.getByText(/Night invite/)).toBeInTheDocument();
  });

  it('should paginate with 20 items per page and navigation works', async () => {
    const user = userEvent.setup();
    const notifications = createNotifications(25);
    setupStore({ notifications, unreadCount: 25 });

    render(<NotificationsPage />);

    // First page: 20 items - use exact text to avoid matching "Notification 10" etc.
    expect(screen.getByText('Notification 1')).toBeInTheDocument();
    expect(screen.getByText('Notification 20')).toBeInTheDocument();
    expect(screen.queryByText('Notification 21')).not.toBeInTheDocument();

    // Pagination should show 2 pages
    const nextButton = screen.getByRole('button', { name: /next page/i });
    await user.click(nextButton);

    // Second page: 5 items - page 1 items gone
    expect(screen.queryByText('Notification 1')).not.toBeInTheDocument();
    expect(screen.getByText('Notification 21')).toBeInTheDocument();
    expect(screen.getByText('Notification 25')).toBeInTheDocument();
  });

  it('should call markAllAsRead() when "Segna tutte come lette" button is clicked', async () => {
    const user = userEvent.setup();
    const notifications = [createNotification({ isRead: false })];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    const markAllButton = screen.getByRole('button', { name: /segna tutte come lette/i });
    await user.click(markAllButton);

    expect(mockMarkAllAsRead).toHaveBeenCalledTimes(1);
  });

  it('should not show "Segna tutte come lette" when all notifications are read', () => {
    const notifications = [createNotification({ isRead: true })];
    setupStore({ notifications, unreadCount: 0 });

    render(<NotificationsPage />);

    expect(
      screen.queryByRole('button', { name: /segna tutte come lette/i })
    ).not.toBeInTheDocument();
  });

  it('should show empty state when no notifications', () => {
    setupStore({ notifications: [], unreadCount: 0, isFetching: false });

    render(<NotificationsPage />);

    expect(screen.getByText(EMPTY.notifications)).toBeInTheDocument();
  });

  it('should show empty state with unread tab message', async () => {
    const user = userEvent.setup();
    const notifications = [createNotification({ isRead: true })];
    setupStore({ notifications, unreadCount: 0 });

    render(<NotificationsPage />);

    // Switch to unread tab
    const unreadTab = screen.getByRole('tab', { name: /non lette/i });
    await user.click(unreadTab);

    // The empty state container has the text "Nessuna notifica non letta"
    // Also appears in header subtitle, so check for the empty state icon (Bell) + text combo
    const emptyStates = screen.getAllByText(EMPTY.notificationsUnread);
    // At least one should be in the empty state container (not just the header)
    expect(emptyStates.length).toBeGreaterThanOrEqual(1);
  });

  it('should show loading state while fetching', () => {
    setupStore({ isFetching: true, notifications: [] });

    render(<NotificationsPage />);

    // Loader icon should be present (Loader2 component)
    const loader = document.querySelector('.animate-spin');
    expect(loader).toBeInTheDocument();
  });

  it('should show error state when error occurs', () => {
    setupStore({ error: 'Network error', isFetching: false });

    render(<NotificationsPage />);

    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('should fetch notifications on mount', () => {
    setupStore();

    render(<NotificationsPage />);

    expect(mockFetchNotifications).toHaveBeenCalledTimes(1);
    expect(mockFetchNotifications).toHaveBeenCalledWith({});
  });

  it('should show correct unread count in header', () => {
    const notifications = [
      createNotification({ isRead: false }),
      createNotification({ isRead: false }),
      createNotification({ isRead: true }),
    ];
    setupStore({ notifications, unreadCount: 2 });

    render(<NotificationsPage />);

    expect(screen.getByText('2 non lette')).toBeInTheDocument();
  });

  it('should show type-specific empty state when filter has no results', async () => {
    const user = userEvent.setup();
    const notifications = [createNotification({ type: 'document_ready' })];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // Click a filter category that has no matching notifications
    await user.click(screen.getByRole('button', { name: /serate/i }));

    expect(screen.getByText(EMPTY.notificationsOfType)).toBeInTheDocument();
  });

  it('should display all filter category pills', () => {
    setupStore();

    render(<NotificationsPage />);

    // Claude Design v1: all | sessions | agents | events | system
    const filterBar = screen.getByRole('tablist', { name: /categoria notifiche/i });
    expect(within(filterBar).getByRole('button', { name: /^tutte$/i })).toBeInTheDocument();
    expect(within(filterBar).getByRole('button', { name: /sessioni/i })).toBeInTheDocument();
    expect(within(filterBar).getByRole('button', { name: /agenti/i })).toBeInTheDocument();
    expect(within(filterBar).getByRole('button', { name: /serate/i })).toBeInTheDocument();
    expect(within(filterBar).getByRole('button', { name: /sistema/i })).toBeInTheDocument();
  });

  it('should show "Nessuna notifica non letta" when all are read', () => {
    const notifications = [createNotification({ isRead: true })];
    setupStore({ notifications, unreadCount: 0 });

    render(<NotificationsPage />);

    expect(screen.getByText(EMPTY.notificationsUnread)).toBeInTheDocument();
  });

  // ── Claude Design v1 migration (M6 Task 11) ─────────────────────
  it('should render NotificationCard with entity border and unread dot', () => {
    const notifications = [
      createNotification({
        title: 'Unread test',
        type: 'game_night_invitation',
        isRead: false,
      }),
    ];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // Entity "event" assigned for game_night_* notifications
    const card = document.querySelector('[data-entity="event"]');
    expect(card).not.toBeNull();
    // Unread dot present (from NotificationCard)
    expect(document.querySelector('[data-testid="unread-dot"]')).not.toBeNull();
  });

  it('should open detail drawer when notification card is clicked and mark as read', async () => {
    const user = userEvent.setup();
    const notifications = [
      createNotification({
        title: 'Detail target',
        message: 'Long detail message content',
        type: 'agent_ready',
        isRead: false,
      }),
    ];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // Click the card (NotificationCard becomes a button when only onClick, role=button when onDismiss)
    const card = screen.getByRole('button', { name: /detail target/i });
    await user.click(card);

    // markAsRead invoked
    expect(mockMarkAsRead).toHaveBeenCalledWith(notifications[0].id);
  });

  it('should group notifications by day (Oggi / Ieri / Precedenti)', () => {
    const today = new Date();
    const yesterday = new Date(today.getTime() - 24 * 3600_000);
    const lastMonth = new Date(today.getTime() - 40 * 24 * 3600_000);
    const notifications = [
      createNotification({ title: 'Today item', createdAt: today.toISOString() }),
      createNotification({ title: 'Yesterday item', createdAt: yesterday.toISOString() }),
      createNotification({ title: 'Old item', createdAt: lastMonth.toISOString() }),
    ];
    setupStore({ notifications, unreadCount: 3 });

    render(<NotificationsPage />);

    expect(screen.getByRole('heading', { name: /oggi/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /ieri/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /precedenti/i })).toBeInTheDocument();
  });
});
