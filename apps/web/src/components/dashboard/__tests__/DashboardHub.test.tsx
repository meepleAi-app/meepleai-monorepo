/**
 * DashboardHub Tests - Issue #3975, #3979
 *
 * Unit tests for DashboardHub component including:
 * - Loading/Success/Error states
 * - Responsive grid layout
 * - Framer Motion animations (mocked)
 * - Lazy loading below-fold sections
 * - Touch-friendly targets
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { DashboardHub } from '../DashboardHub';
import { fetchDashboardData } from '@/lib/api/dashboard';
import type { DashboardData } from '@/types/dashboard';

// Mock the dashboard API module
vi.mock('@/lib/api/dashboard', () => ({
  fetchDashboardData: vi.fn(),
}));

// Mock useCatalogTrending hook (CatalogTrending widget fetches independently)
vi.mock('@/hooks/useCatalogTrending', () => ({
  useCatalogTrending: vi.fn(() => ({
    data: [],
    isLoading: false,
    error: null,
  })),
}));

// Mock react-intersection-observer (useInView for lazy loading)
const mockUseInView = vi.fn();
vi.mock('react-intersection-observer', () => ({
  useInView: (...args: unknown[]) => mockUseInView(...args),
}));

// Mock useReducedMotion from our animations lib
vi.mock('@/lib/animations', () => ({
  useReducedMotion: () => false,
}));

const mockFetchDashboardData = vi.mocked(fetchDashboardData);

// ============================================================================
// Mock Data
// ============================================================================

const mockDashboardData: DashboardData = {
  user: {
    id: '550e8400-e29b-41d4-a716-446655440000', // Valid UUID
    username: 'TestUser',
    email: 'test@example.com',
  },
  stats: {
    libraryCount: 127,
    playedLast30Days: 23,
    chatCount: 12,
    wishlistCount: 15,
    currentStreak: 7,
  },
  activeSessions: [
    {
      id: 'session-1',
      gameName: 'Catan',
      gameId: 'game-1',
      coverUrl: 'https://example.com/catan.jpg',
      players: { current: 3, total: 4 },
      progress: { turn: 12, duration: '45min' },
      lastActivity: new Date('2026-02-09T10:00:00Z'),
    },
  ],
  librarySnapshot: {
    quota: { used: 127, total: 200 },
    topGames: [
      {
        id: 'game-1',
        title: 'Catan',
        coverUrl: 'https://example.com/catan.jpg',
        rating: 5,
        playCount: 45,
      },
    ],
  },
  recentActivity: [
    {
      id: 'activity-1',
      type: 'game_added',
      gameId: 'game-1',
      gameName: 'Wingspan',
      timestamp: new Date('2026-02-09T15:00:00Z'),
    },
  ],
  chatHistory: [
    {
      id: 'chat-1',
      topic: 'Regole Wingspan',
      messageCount: 8,
      timestamp: new Date('2026-02-09T14:30:00Z'),
    },
  ],
};

// ============================================================================
// Test Helpers
// ============================================================================

function createQueryWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

/** Helper: mock useInView to return inView=true (sections visible) */
function mockAllSectionsVisible() {
  mockUseInView.mockReturnValue({ ref: { current: null }, inView: true });
}

/** Helper: mock useInView to return inView=false (sections not visible) */
function mockAllSectionsHidden() {
  mockUseInView.mockReturnValue({ ref: { current: null }, inView: false });
}

// ============================================================================
// Tests
// ============================================================================

describe('DashboardHub', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: all lazy sections visible (so existing tests work)
    mockAllSectionsVisible();
  });

  describe('Loading State', () => {
    it('shows skeleton during data fetch', () => {
      // Mock loading state
      mockFetchDashboardData.mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      // Should show skeleton elements (divs with animate-pulse class)
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });
  });

  describe('Success State', () => {
    beforeEach(() => {
      mockFetchDashboardData.mockResolvedValue(mockDashboardData);
    });

    it('renders all dashboard sections with data', async () => {
      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // Hero Stats should show username
        expect(screen.getByText(/TestUser/i)).toBeInTheDocument();

        // Active sessions should show game name (appears in sessions + library)
        expect(screen.getAllByText(/Catan/i).length).toBeGreaterThanOrEqual(1);
      });
    });

    it('renders all 8 main sections', async () => {
      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // Count only direct child sections of the grid (child components also render <section>)
        const grid = container.querySelector('.grid');
        const directSections = grid?.querySelectorAll(':scope > section');
        expect(directSections?.length).toBe(8); // Hero, Sessions, Library, Activity, Wishlist, CatalogTrending, Chat, QuickActions
      });
    });

    it('uses correct responsive grid classes', async () => {
      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        const grid = container.querySelector('.grid');
        expect(grid?.className).toContain('grid-cols-1'); // Mobile
        expect(grid?.className).toContain('md:grid-cols-2'); // Tablet
        expect(grid?.className).toContain('lg:grid-cols-3'); // Desktop
      });
    });
  });

  describe('Error State', () => {
    it('shows error fallback when API fails', async () => {
      mockFetchDashboardData.mockRejectedValue(new Error('API Error'));

      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      // Hook has retry: 2 with exponential backoff (~3-6s), so we need a long timeout
      await waitFor(
        () => {
          expect(screen.getByText('Dashboard Error')).toBeInTheDocument();
          expect(screen.getByText(/try again/i)).toBeInTheDocument();
        },
        { timeout: 15000 },
      );
    }, 20000);

    it('shows error fallback when data is null', async () => {
      mockFetchDashboardData.mockResolvedValue(null as any);

      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Dashboard Error')).toBeInTheDocument();
        expect(screen.getByText(/no dashboard data available/i)).toBeInTheDocument();
      });
    });
  });

  describe('Data Adapters', () => {
    beforeEach(() => {
      mockFetchDashboardData.mockResolvedValue(mockDashboardData);
    });

    it('adapts API data to component formats correctly', async () => {
      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // Active sessions adapted correctly (appears in sessions + library)
        expect(screen.getAllByText(/Catan/i).length).toBeGreaterThanOrEqual(1);

        // Activity adapted correctly (should show formatted activity)
        const activityText = screen.queryByText(/Added.*Wingspan/i) || screen.queryByText(/Wingspan/i);
        expect(activityText).toBeInTheDocument();

        // Chat adapted correctly
        expect(screen.getByText(/Regole Wingspan/i)).toBeInTheDocument();
      });
    });
  });

  describe('Responsive Layout', () => {
    beforeEach(() => {
      mockFetchDashboardData.mockResolvedValue(mockDashboardData);
    });

    it('applies full-width class to hero and sessions sections', async () => {
      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        const fullWidthSections = container.querySelectorAll('.col-span-full');
        expect(fullWidthSections.length).toBeGreaterThanOrEqual(2); // Hero + Active Sessions
      });
    });

    it('applies asymmetric layout to library and activity sections', async () => {
      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // Library should be 1 col on desktop
        const librarySections = container.querySelectorAll('.lg\\:col-span-1');
        expect(librarySections.length).toBeGreaterThan(0);

        // Activity should be 2 cols on desktop
        const activitySections = container.querySelectorAll('.lg\\:col-span-2');
        expect(activitySections.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Lazy Loading (Issue #3979)', () => {
    beforeEach(() => {
      mockFetchDashboardData.mockResolvedValue(mockDashboardData);
    });

    it('shows skeleton placeholder for below-fold sections when not in view', async () => {
      mockAllSectionsHidden();

      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // Above-fold sections (Hero, Sessions) should still render
        expect(screen.getByText(/TestUser/i)).toBeInTheDocument();

        // Below-fold sections show skeleton placeholders
        // LazySection renders animate-pulse divs when inView=false
        const grid = container.querySelector('.grid');
        const skeletons = grid?.querySelectorAll('.animate-pulse');
        // 6 below-fold sections: Library, Activity, Wishlist, CatalogTrending, Chat, QuickActions
        expect(skeletons?.length).toBe(6);
      });
    });

    it('renders content for below-fold sections when in view', async () => {
      mockAllSectionsVisible();

      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // All sections should render their content
        expect(screen.getByText(/TestUser/i)).toBeInTheDocument();
        expect(screen.getAllByText(/Catan/i).length).toBeGreaterThanOrEqual(1);
        expect(screen.getByText(/Regole Wingspan/i)).toBeInTheDocument();
      });
    });

    it('calls useInView with triggerOnce and threshold options', async () => {
      render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        // useInView should be called for each LazySection (4 below-fold)
        expect(mockUseInView).toHaveBeenCalled();
        const firstCall = mockUseInView.mock.calls[0][0];
        expect(firstCall).toEqual(
          expect.objectContaining({
            triggerOnce: true,
            threshold: 0.1,
          }),
        );
      });
    });
  });

  describe('Touch Targets (Issue #3979)', () => {
    it('error fallback buttons have minimum 44px touch targets', async () => {
      mockFetchDashboardData.mockResolvedValue(null as any);

      const { container } = render(<DashboardHub />, { wrapper: createQueryWrapper() });

      await waitFor(() => {
        const tryAgainButton = screen.getByText(/try again/i);
        expect(tryAgainButton.className).toContain('min-h-[44px]');
        expect(tryAgainButton.className).toContain('min-w-[44px]');
      });
    });
  });
});
