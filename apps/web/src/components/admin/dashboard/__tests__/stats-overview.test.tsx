import { screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { StatsOverview } from '../stats-overview';
import * as adminClientModule from '@/lib/api/admin-client';

// Mock admin client
vi.mock('@/lib/api/admin-client', () => ({
  adminClient: {
    getStats: vi.fn(),
  },
}));

describe('StatsOverview', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and link', () => {
    renderWithQuery(<StatsOverview />);

    expect(screen.getByRole('heading', { name: /collection overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view details/i })).toHaveAttribute(
      'href',
      '/admin/collection/overview'
    );
  });

  it('displays loading skeletons while fetching data', () => {
    vi.mocked(adminClientModule.adminClient.getStats).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    renderWithQuery(<StatsOverview />);

    // Should show 4 StatCard components in loading state
    const statCards = screen.getAllByTestId('stat-card-loading');
    expect(statCards).toHaveLength(4);
  });

  it('displays stat cards with data when loaded', async () => {
    vi.mocked(adminClientModule.adminClient.getStats).mockResolvedValue({
      totalGames: 1247,
      publishedGames: 1156,
      pendingGames: 23,
      totalUsers: 8542,
      activeUsers: 3891,
      newUsers: 156,
      approvalRate: 94,
      pendingApprovals: 23,
      recentSubmissions: 47,
    });

    renderWithQuery(<StatsOverview />);

    // Wait for data to load
    await screen.findByText('1247');
    await screen.findByText('8542');
    await screen.findByText('94%');
    await screen.findByText('47');

    expect(screen.getByText(/shared games/i)).toBeInTheDocument();
    expect(screen.getByText(/community/i)).toBeInTheDocument();
    expect(screen.getByText(/approval rate/i)).toBeInTheDocument();
    expect(screen.getByText(/recent activity/i)).toBeInTheDocument();
  });

  it('shows trend values when available', async () => {
    vi.mocked(adminClientModule.adminClient.getStats).mockResolvedValue({
      totalGames: 1247,
      publishedGames: 1156,
      totalUsers: 8542,
      activeUsers: 3891,
      pendingApprovals: 23,
    });

    renderWithQuery(<StatsOverview />);

    await screen.findByText(/1156 published/i);
    await screen.findByText(/3891 active/i);
    await screen.findByText(/23 pending/i);
  });

  it('handles API errors gracefully', async () => {
    vi.mocked(adminClientModule.adminClient.getStats).mockRejectedValue(new Error('API Error'));

    renderWithQuery(<StatsOverview />);

    // Component should not crash, React Query handles error state
    expect(screen.getByRole('heading', { name: /collection overview/i })).toBeInTheDocument();
  });

  it('uses default values when data is missing', async () => {
    vi.mocked(adminClientModule.adminClient.getStats).mockResolvedValue({});

    renderWithQuery(<StatsOverview />);

    await screen.findByText('0'); // Should show 0 for missing values
  });
});
