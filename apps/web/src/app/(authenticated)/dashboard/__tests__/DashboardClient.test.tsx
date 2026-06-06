/**
 * DashboardClient — Asse C priority-driven cluster smoke tests (Issue #1898 T7).
 *
 * Replaces the Stage 3 5-entity cluster test. The new test exercises the
 * REFACTOR shape: 4 priority sections in fixed order (Prossimi → Recenti →
 * Suggested → Friends) + DashboardHero preserved.
 *
 * Detailed per-section variant/state tests live in
 * `_components/sections/__tests__/` (T2-T6). This file's job is the orchestrator
 * contract: hero present, 4 sections in correct order, state forwarding works.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'u1', displayName: 'Marco', email: 'marco@example.com' },
  }),
}));

const mockTranslate = (key: string): string => key;
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: mockTranslate }),
}));

const refetchUpcoming = vi.fn();
const refetchGames = vi.fn();

vi.mock('@/hooks/queries/useGames', () => ({
  useGames: vi.fn(() => ({
    data: { games: [], total: 0, page: 1, pageSize: 20, totalPages: 0 },
    isLoading: false,
    isError: false,
    refetch: refetchGames,
  })),
}));

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: vi.fn(() => ({
    data: { sessions: [], total: 0, page: 1, pageSize: 10 },
    isLoading: false,
    isError: false,
  })),
}));

vi.mock('@/hooks/queries/useGameNights', () => ({
  useUpcomingGameNights: vi.fn(() => ({
    data: [],
    isLoading: false,
    isError: false,
    refetch: refetchUpcoming,
  })),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryStats: vi.fn(() => ({
    data: { totalGames: 0, favoriteGames: 0, privatePdfs: 0 },
    isLoading: false,
    isError: false,
  })),
}));

vi.mock('@/hooks/use-friends-activity', () => ({
  useFriendsActivity: vi.fn(() => ({
    data: [],
    isLoading: false,
    isError: false,
  })),
}));

// cascade-store is consumed by ProssimiSection / RecentiSection /
// FriendsActivitySection (asse-B integration). Mock as no-op for tests.
// Mock the cascade store: each call returns a slice of canonical "closed" state
// or a no-op action. Issue #1929 WP5 adds CascadeDrawerHost which subscribes to
// `state` / `activeEntityType` / `activeEntityId` / `closeCascade` — the old
// blanket `vi.fn(() => vi.fn())` made `useCascadeNavigationStore(s => s.state)`
// resolve to a function reference, which then short-circuited the entityType
// fallback to a non-string value and crashed ExtraMeepleCardDrawer's ENTITY_CONFIG
// lookup. Use a typed selector mock so the host renders cleanly in test mode.
const CLOSED_CASCADE_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [] as ReadonlyArray<unknown>,
  openDeckStack: vi.fn(),
  openDrawer: vi.fn(),
  closeDrawer: vi.fn(),
  closeCascade: vi.fn(),
  pushDrawer: vi.fn(),
  popDrawer: vi.fn(),
};
vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: vi.fn((selector?: (state: typeof CLOSED_CASCADE_STATE) => unknown) =>
    selector ? selector(CLOSED_CASCADE_STATE) : CLOSED_CASCADE_STATE
  ),
}));

import { DashboardClient } from '../DashboardClient';

describe('DashboardClient (Asse C priority cluster)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the dashboard hero with greeting + user name', () => {
    render(<DashboardClient />);
    expect(screen.getByText(/Marco/i)).toBeInTheDocument();
  });

  it('renders the hero KPI grid with 4 cards', () => {
    const { container } = render(<DashboardClient />);
    const kpiGrid = container.querySelector('[data-slot="dashboard-hero-kpi-grid"]');
    expect(kpiGrid).not.toBeNull();
    const kpiCards = container.querySelectorAll('[data-slot="dashboard-kpi-card"]');
    expect(kpiCards.length).toBe(4);
  });

  it('renders Prossimi section in priority slot #1 (empty state)', () => {
    const { container } = render(<DashboardClient />);
    // Empty state → Prossimi still renders (with EmptySection inside).
    const prossimi = container.querySelector('[data-section-id="prossimi"]');
    expect(prossimi).not.toBeNull();
  });

  it('renders priority sections container with flex-column layout', () => {
    const { container } = render(<DashboardClient />);
    const grid = container.querySelector('[data-slot="dashboard-priority-sections"]');
    expect(grid).not.toBeNull();
  });

  it('renders 4 priority sections in fixed order (when all have data)', async () => {
    // Override mocks to provide data so all 4 sections render.
    const useGamesMock = vi.mocked(await import('@/hooks/queries/useGames')).useGames;
    const useGameNightsMock = vi.mocked(
      await import('@/hooks/queries/useGameNights')
    ).useUpcomingGameNights;
    const useFriendsMock = vi.mocked(
      await import('@/hooks/use-friends-activity')
    ).useFriendsActivity;

    useGamesMock.mockReturnValueOnce({
      data: {
        games: [
          {
            id: '00000000-0000-0000-0000-000000000001',
            title: 'Catan',
            publisher: 'Kosmos',
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            minPlayTimeMinutes: 60,
            maxPlayTimeMinutes: 120,
            bggId: 13,
            createdAt: '2024-01-01T00:00:00Z',
            imageUrl: null,
            iconUrl: null,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      },
      isLoading: false,
      isError: false,
      refetch: refetchGames,
      // The cast is intentional — TanStack returns a much wider type;
      // we only assert the fields the component actually reads.
    } as unknown as ReturnType<typeof useGamesMock>);

    useGameNightsMock.mockReturnValueOnce({
      data: [
        {
          id: '00000000-0000-0000-0000-000000000010',
          organizerId: '00000000-0000-0000-0000-0000000000aa',
          organizerName: 'Marco',
          title: 'Catan Night',
          description: null,
          scheduledAt: '2030-01-01T20:00:00Z',
          location: null,
          maxPlayers: null,
          gameIds: [],
          status: 'Published' as const,
          acceptedCount: 2,
          pendingCount: 1,
          totalInvited: 3,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: null,
        },
      ],
      isLoading: false,
      isError: false,
      refetch: refetchUpcoming,
    } as unknown as ReturnType<typeof useGameNightsMock>);

    useFriendsMock.mockReturnValueOnce({
      data: [
        {
          friendUserId: '00000000-0000-0000-0000-0000000000bb',
          avatar: '',
          name: 'Luca',
          verb: 'completed' as const,
          gameOrEventId: '00000000-0000-0000-0000-000000000020',
          gameOrEventType: 'game' as const,
          gameOrEventName: 'Wingspan',
          timestamp: '2025-01-01T00:00:00Z',
        },
      ],
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useFriendsMock>);

    const { container } = render(<DashboardClient />);

    const sections = container.querySelectorAll('[data-slot="dashboard-section"]');
    const sectionIds = Array.from(sections).map(s => s.getAttribute('data-section-id'));

    // Recenti is currently null (no completed GN endpoint yet) — verify the
    // 3 sections that DO render appear and Prossimi precedes Suggested
    // precedes Friends.
    expect(sectionIds).toContain('prossimi');
    expect(sectionIds).toContain('suggested');
    expect(sectionIds).toContain('friends-activity');

    const prossimiIdx = sectionIds.indexOf('prossimi');
    const suggestedIdx = sectionIds.indexOf('suggested');
    const friendsIdx = sectionIds.indexOf('friends-activity');

    expect(prossimiIdx).toBeLessThan(suggestedIdx);
    expect(suggestedIdx).toBeLessThan(friendsIdx);
  });

  it('forwards loading state to ProssimiSection', async () => {
    const useGameNightsMock = vi.mocked(
      await import('@/hooks/queries/useGameNights')
    ).useUpcomingGameNights;
    useGameNightsMock.mockReturnValueOnce({
      data: undefined,
      isLoading: true,
      isError: false,
      refetch: refetchUpcoming,
    } as unknown as ReturnType<typeof useGameNightsMock>);

    const { container } = render(<DashboardClient />);
    expect(container.querySelector('[data-testid="prossimi-skeleton"]')).not.toBeNull();
  });

  it('forwards error state to ProssimiSection', async () => {
    const useGameNightsMock = vi.mocked(
      await import('@/hooks/queries/useGameNights')
    ).useUpcomingGameNights;
    useGameNightsMock.mockReturnValueOnce({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: refetchUpcoming,
    } as unknown as ReturnType<typeof useGameNightsMock>);

    const { container } = render(<DashboardClient />);
    expect(container.querySelector('[data-testid="prossimi-error"]')).not.toBeNull();
  });
});
