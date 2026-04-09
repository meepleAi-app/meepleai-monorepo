import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

// Mock the v2 composition so we don't need to mock all its downstream deps
vi.mock('../v2', () => ({
  DashboardClientV2: () => <div data-testid="dashboard-v2">V2 Dashboard</div>,
}));

// Mock legacy dependencies that would otherwise break in jsdom
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/dashboard',
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { id: 'u1', displayName: 'Marco' } }),
}));

vi.mock('@/lib/stores/dashboard-store', () => ({
  useDashboardStore: () => ({
    stats: null,
    recentSessions: [],
    games: [],
    totalGamesCount: 0,
    trendingGames: [],
    isLoadingStats: false,
    isLoadingGames: false,
    isLoadingTrending: false,
    isLoadingSessions: false,
    fetchStats: vi.fn(),
    fetchGames: vi.fn(),
    fetchRecentSessions: vi.fn(),
    fetchTrendingGames: vi.fn(),
    updateFilters: vi.fn(),
  }),
}));

vi.mock('@/components/dashboard/StatsRow', () => ({
  StatsRow: () => <div data-testid="stats-row" />,
}));

vi.mock('@/components/dashboard/WelcomeHero', () => ({
  WelcomeHero: () => <div data-testid="welcome-hero" />,
}));

vi.mock('@/components/layout/FloatingActionPill', () => ({
  FloatingActionPill: () => <div data-testid="floating-action-pill" />,
}));

const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

describe('DashboardClient feature flag branch', () => {
  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
    vi.resetModules();
  });

  it('renders DashboardClientV2 when flag is on', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    vi.resetModules();
    const mod = await import('../dashboard-client');
    const DashboardClient =
      (mod as { DashboardClient?: React.ComponentType }).DashboardClient ??
      (mod as { default?: React.ComponentType }).default;
    if (!DashboardClient) throw new Error('DashboardClient export not found');
    render(<DashboardClient />);
    expect(screen.getByTestId('dashboard-v2')).toBeInTheDocument();
  });

  it('does not render DashboardClientV2 when flag is off', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
    vi.resetModules();
    const mod = await import('../dashboard-client');
    const DashboardClient =
      (mod as { DashboardClient?: React.ComponentType }).DashboardClient ??
      (mod as { default?: React.ComponentType }).default;
    if (!DashboardClient) throw new Error('DashboardClient export not found');
    render(<DashboardClient />);
    expect(screen.queryByTestId('dashboard-v2')).not.toBeInTheDocument();
  });
});
