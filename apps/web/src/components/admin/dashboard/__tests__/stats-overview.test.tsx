import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { StatsOverview } from '../stats-overview';

// Mock admin client
vi.mock('@/lib/api/admin-client', () => ({
  adminClient: {
    getStats: vi.fn(),
  },
}));

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  );
}

describe('StatsOverview', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and link', () => {
    renderWithQueryClient(<StatsOverview />);

    expect(screen.getByRole('heading', { name: /collection overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view details/i })).toHaveAttribute(
      'href',
      '/admin/collection/overview'
    );
  });

  it('displays loading skeletons while fetching data', () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getStats.mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    renderWithQueryClient(<StatsOverview />);

    // Should show 4 StatCard components in loading state
    const statCards = screen.getAllByTestId('stat-card-loading');
    expect(statCards).toHaveLength(4);
  });

  it('displays stat cards with data when loaded', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getStats.mockResolvedValue({
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

    renderWithQueryClient(<StatsOverview />);

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
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getStats.mockResolvedValue({
      totalGames: 1247,
      publishedGames: 1156,
      totalUsers: 8542,
      activeUsers: 3891,
      pendingApprovals: 23,
    });

    renderWithQueryClient(<StatsOverview />);

    await screen.findByText(/1156 published/i);
    await screen.findByText(/3891 active/i);
    await screen.findByText(/23 pending/i);
  });

  it('handles API errors gracefully', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getStats.mockRejectedValue(new Error('API Error'));

    renderWithQueryClient(<StatsOverview />);

    // Component should not crash, React Query handles error state
    expect(screen.getByRole('heading', { name: /collection overview/i })).toBeInTheDocument();
  });

  it('uses default values when data is missing', async () => {
    const { adminClient } = require('@/lib/api/admin-client');
    adminClient.getStats.mockResolvedValue({});

    renderWithQueryClient(<StatsOverview />);

    await screen.findByText('0'); // Should show 0 for missing values
  });
});
