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
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';

import NotificationsPage from '../page';
import type { NotificationDto } from '@/lib/api';
import { logger } from '@/lib/logger';
import { EMPTY } from '../../../../__tests__/fixtures/test-strings';
// #1816 P3-i18n: CatalogPagination (rendered when notifications > 20) calls
// useTranslation which requires IntlProvider in the tree. Use renderWithIntl
// for all tests so the paginated case has the provider available.
import { renderWithIntl } from '../../../../__tests__/fixtures/common-fixtures';

const render = renderWithIntl;

expect.extend(toHaveNoViolations);

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

  it('toggles unread-only view via the counter button (#2181)', async () => {
    const user = userEvent.setup();
    const notifications = [
      createNotification({ title: 'Read notification', isRead: true }),
      createNotification({ title: 'Unread notification', isRead: false }),
    ];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // Default view shows both (toggle off)
    expect(screen.getByText(/Read notification/)).toBeInTheDocument();
    expect(screen.getByText(/Unread notification/)).toBeInTheDocument();

    // Click the header counter to turn the unread-only toggle on
    const toggle = screen.getByTestId('notifications-unread-toggle');
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'true');

    // Only the unread notification should be visible
    expect(screen.queryByText(/Read notification/)).not.toBeInTheDocument();
    expect(screen.getByText(/Unread notification/)).toBeInTheDocument();

    // Click again to restore the full list
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByText(/Read notification/)).toBeInTheDocument();
  });

  it('disables the unread-toggle when there are no unread items (#2181)', () => {
    const notifications = [createNotification({ title: 'Read', isRead: true })];
    setupStore({ notifications, unreadCount: 0 });

    render(<NotificationsPage />);

    const toggle = screen.getByTestId('notifications-unread-toggle');
    expect(toggle).toBeDisabled();
    expect(toggle).toHaveTextContent(/Nessuna notifica non letta/);
  });

  it('does not render the legacy "Tutte / Non lette" role=tab pair (#2181)', () => {
    setupStore({ notifications: [], unreadCount: 0 });
    render(<NotificationsPage />);

    expect(screen.queryByRole('tab', { name: /^Tutte$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: /non lette/i })).not.toBeInTheDocument();
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

  it('disables "Segna tutte come lette" when there are no unread notifications (#2181)', () => {
    const notifications = [createNotification({ isRead: true })];
    setupStore({ notifications, unreadCount: 0 });

    render(<NotificationsPage />);

    // Issue #2181: the CTA is now persistent and disabled rather than hidden,
    // so the user knows the action exists even before any unread arrives.
    const markAllButton = screen.getByRole('button', { name: /segna tutte come lette/i });
    expect(markAllButton).toBeInTheDocument();
    expect(markAllButton).toBeDisabled();
  });

  it('should show empty state when no notifications (#2183)', () => {
    setupStore({ notifications: [], unreadCount: 0, isFetching: false });

    render(<NotificationsPage />);

    // Heading: explicit "no notifications yet" headline (#2183 contextual copy)
    expect(screen.getByText(EMPTY.notificationsEmptyDefault)).toBeInTheDocument();
    // CTA to /notifications/preferences must always render
    const cta = screen.getByTestId('notifications-empty-preferences-cta');
    expect(cta).toHaveAttribute('href', '/notifications/preferences');
  });

  it('should show empty state with unread message when unread-only toggle is on (#2181)', async () => {
    const user = userEvent.setup();
    // Need at least one unread item so the toggle is enabled; once toggled
    // we mark it as read mid-test? Simpler: start with one unread, mark it
    // first so the count is 1 and the toggle button is reachable, then turn
    // the toggle on and assert the empty-state copy is rendered.
    const notifications = [
      createNotification({ title: 'Read item', isRead: true }),
      createNotification({ title: 'Single unread', isRead: false }),
    ];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    const toggle = screen.getByTestId('notifications-unread-toggle');
    await user.click(toggle);

    // Only the single unread notification should remain visible (no empty state)
    expect(screen.getByText(/Single unread/)).toBeInTheDocument();

    // Now simulate that the user marked it as read by re-rendering with 0 unread
    // and the toggle staying on. Easiest path: assert that with 0 filtered + toggle on,
    // the empty-state copy mentions "non letta".
    setupStore({
      notifications: [createNotification({ title: 'All read', isRead: true })],
      unreadCount: 0,
    });
    render(<NotificationsPage />);
    const newToggle = screen.getAllByTestId('notifications-unread-toggle').at(-1)!;
    // Toggle is disabled (unreadCount === 0), so the "all-read" message
    // lives both in the toggle label and in the empty-state copy (#2181
    // toggle + #2183 contextual empty-state heading).
    expect(newToggle).toBeDisabled();
    expect(screen.getAllByText(EMPTY.notificationsUnread).length).toBeGreaterThanOrEqual(1);
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

  it('should show type-specific empty state when filter has no results (#2183)', async () => {
    const user = userEvent.setup();
    const notifications = [createNotification({ type: 'document_ready' })];
    setupStore({ notifications, unreadCount: 1 });

    render(<NotificationsPage />);

    // Click a filter category that has no matching notifications
    await user.click(screen.getByRole('button', { name: /serate/i }));

    expect(screen.getByText(EMPTY.notificationsEmptyCategory)).toBeInTheDocument();
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

  // ── detail.link safety (#2182) ───────────────────────────────────
  describe('Notifications detail.link click handler (#2182)', () => {
    it('calls window.location.assign for safe relative path', async () => {
      const user = userEvent.setup();
      const assignMock = vi.fn();
      const originalLocation = window.location;
      Object.defineProperty(window, 'location', {
        configurable: true,
        writable: true,
        value: { ...originalLocation, assign: assignMock },
      });

      try {
        const notifications = [
          createNotification({
            title: 'Safe link notification',
            link: '/games/abc-123',
            isRead: true,
          }),
        ];
        setupStore({ notifications, unreadCount: 0 });

        render(<NotificationsPage />);

        // Open the detail drawer by clicking the card
        const card = screen.getByRole('button', { name: /safe link notification/i });
        await user.click(card);

        // The "Apri" button should now be rendered inside the open drawer
        const apriBtn = await screen.findByRole('button', { name: /^apri$/i });
        await user.click(apriBtn);

        expect(assignMock).toHaveBeenCalledWith('/games/abc-123');
      } finally {
        Object.defineProperty(window, 'location', {
          configurable: true,
          writable: true,
          value: originalLocation,
        });
      }
    });

    it('does NOT call window.location.assign for external URL', async () => {
      const user = userEvent.setup();
      const assignMock = vi.fn();
      const warnSpy = vi.spyOn(logger, 'warn').mockImplementation(() => undefined);
      const originalLocation = window.location;
      Object.defineProperty(window, 'location', {
        configurable: true,
        writable: true,
        value: { ...originalLocation, assign: assignMock },
      });

      try {
        const notifications = [
          createNotification({
            title: 'Unsafe link notification',
            link: 'https://evil.com',
            isRead: true,
          }),
        ];
        setupStore({ notifications, unreadCount: 0 });

        render(<NotificationsPage />);

        // Open detail drawer
        const card = screen.getByRole('button', { name: /unsafe link notification/i });
        await user.click(card);

        // The "Apri" button renders because detail.link is truthy
        const apriBtn = await screen.findByRole('button', { name: /^apri$/i });
        await user.click(apriBtn);

        // window.location.assign must NOT be called for an external URL
        expect(assignMock).not.toHaveBeenCalled();
        // Rejection must be logged via logger.warn
        expect(warnSpy).toHaveBeenCalledWith(
          'Rejected unsafe detail.link in notification',
          expect.objectContaining({
            metadata: expect.objectContaining({ linkMasked: 'https://evil.com' }),
          })
        );
      } finally {
        warnSpy.mockRestore();
        Object.defineProperty(window, 'location', {
          configurable: true,
          writable: true,
          value: originalLocation,
        });
      }
    });
  });

  // ── axe AA gate (#2955 Fase 3) ───────────────────────────────────
  // The restored per-entity coloring puts several primitives on this
  // consumer surface at once: the 5 entity-coloured filter `Btn` pills,
  // one `NotificationCard` per entity, the CTA `Btn`, and (when opened)
  // the per-entity detail `Drawer`. This guards the blocking axe AA gate
  // against a regression when they all render together.
  describe('axe AA gate (#2955 Fase 3)', () => {
    const mixedEntityNotifications = () => [
      createNotification({ title: 'Session ended', type: 'session_terminated', isRead: false }),
      createNotification({ title: 'Agent ready', type: 'agent_ready', isRead: false }),
      createNotification({ title: 'Night invite', type: 'game_night_invitation', isRead: true }),
      createNotification({ title: 'PDF ready', type: 'document_ready', isRead: false }),
      createNotification({ title: 'Badge earned', type: 'badge_earned', isRead: true }),
      createNotification({ title: 'Rate limit', type: 'rate_limit_reached', isRead: false }),
    ];

    it('has no violations across the multi-entity NotificationCard list', async () => {
      setupStore({ notifications: mixedEntityNotifications(), unreadCount: 4 });
      const { container } = render(<NotificationsPage />);
      // Scope to the per-entity NotificationCard list (all six sample
      // notifications land in the "Oggi" group), which is the surface #2955
      // recoloured — six entity-coloured cards rendered together.
      // NOTE: a whole-page scan additionally trips a PRE-EXISTING, coloring-
      // unrelated `aria-required-children` violation on the filter bar
      // (`role="tablist"` holding <button> children, page.tsx:281). That is a
      // real finding surfaced separately, not a regression from the restored
      // coloring, and fixing it is a source change out of scope for this
      // test-only Fase 3.
      const cardList = container.querySelector('section[aria-labelledby="notif-group-oggi"]');
      expect(cardList).not.toBeNull();
      expect(await axe(cardList as HTMLElement)).toHaveNoViolations();
    });

    it('has no violations with the per-entity detail Drawer open', async () => {
      const user = userEvent.setup();
      setupStore({
        notifications: [
          createNotification({
            title: 'Drawer target',
            message: 'Detail body content',
            type: 'agent_ready',
            link: '/games/abc-123',
            isRead: true,
          }),
        ],
        unreadCount: 0,
      });
      render(<NotificationsPage />);

      // The card renders as role=button when only onClick is wired; clicking
      // opens the detail Drawer. "Apri" only exists inside the open drawer
      // (link is truthy), so its presence confirms the per-entity Drawer +
      // Btn are mounted before we scan.
      await user.click(screen.getByRole('button', { name: /drawer target/i }));
      await screen.findByRole('button', { name: /^apri$/i });

      // The Drawer portals to document.body, so scan the whole document.
      expect(await axe(document.body)).toHaveNoViolations();
    });
  });
});
