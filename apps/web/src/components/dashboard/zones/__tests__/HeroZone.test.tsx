/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { HeroZone } from '../HeroZone';

// ---------------------------------------------------------------------------
// Mock useDashboardData
// ---------------------------------------------------------------------------

const mockUseDashboardData = vi.fn();

vi.mock('@/hooks/useDashboardData', () => ({
  useDashboardData: () => mockUseDashboardData(),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = createQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------

const MOCK_DASHBOARD_DATA = {
  user: {
    id: 'user-1',
    username: 'Marco',
    email: 'marco@example.com',
  },
  stats: {
    libraryCount: 42,
    playedLast30Days: 12,
    chatCount: 5,
    wishlistCount: 3,
    currentStreak: 4,
  },
  activeSessions: [],
  librarySnapshot: { quota: { used: 42, total: 200 }, topGames: [] },
  recentActivity: [],
  chatHistory: [],
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('HeroZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders greeting with user name', () => {
    mockUseDashboardData.mockReturnValue({
      data: MOCK_DASHBOARD_DATA,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    const heading = screen.getByRole('heading', { level: 2 });
    expect(heading.textContent).toContain('Marco');
    // Should contain one of the time-based greetings
    expect(heading.textContent).toMatch(/Buongiorno|Buon pomeriggio|Buonasera/);
  });

  it('shows skeleton when loading', () => {
    mockUseDashboardData.mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    renderWithProviders(<HeroZone />);

    expect(screen.getByTestId('hero-zone-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('hero-zone')).not.toBeInTheDocument();
  });

  it('has dashboard-hero class for morph animation', () => {
    mockUseDashboardData.mockReturnValue({
      data: MOCK_DASHBOARD_DATA,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    const zone = screen.getByTestId('hero-zone');
    expect(zone.className).toContain('dashboard-hero');
  });

  it('skeleton also has dashboard-hero class', () => {
    mockUseDashboardData.mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    renderWithProviders(<HeroZone />);

    const skeleton = screen.getByTestId('hero-zone-skeleton');
    expect(skeleton.className).toContain('dashboard-hero');
  });

  it('handles null dashboard data gracefully', () => {
    mockUseDashboardData.mockReturnValue({
      data: null,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    const zone = screen.getByTestId('hero-zone');
    expect(zone).toBeInTheDocument();
    // Should still show greeting without username
    const heading = screen.getByRole('heading', { level: 2 });
    expect(heading.textContent).toMatch(/Buongiorno|Buon pomeriggio|Buonasera/);
    // Should not crash — no stats summary shown
    expect(heading.textContent).not.toContain('undefined');
  });

  it('handles undefined dashboard data gracefully', () => {
    mockUseDashboardData.mockReturnValue({
      data: undefined,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    const zone = screen.getByTestId('hero-zone');
    expect(zone).toBeInTheDocument();
  });

  it('shows welcome prompt for new user with zero games', () => {
    mockUseDashboardData.mockReturnValue({
      data: {
        ...MOCK_DASHBOARD_DATA,
        stats: {
          ...MOCK_DASHBOARD_DATA.stats,
          libraryCount: 0,
        },
      },
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    expect(screen.getByText(/Cerca un gioco/)).toBeInTheDocument();
  });

  it('shows stats summary for existing user', () => {
    mockUseDashboardData.mockReturnValue({
      data: MOCK_DASHBOARD_DATA,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    expect(screen.getByText(/42 giochi in libreria/)).toBeInTheDocument();
    expect(screen.getByText(/12 partite questo mese/)).toBeInTheDocument();
  });

  it('does not show welcome prompt for user with games', () => {
    mockUseDashboardData.mockReturnValue({
      data: MOCK_DASHBOARD_DATA,
      isLoading: false,
    });

    renderWithProviders(<HeroZone />);

    expect(screen.queryByTestId('hero-search-cta')).not.toBeInTheDocument();
  });
});
