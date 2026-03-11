import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetOverviewStats = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    admin: {
      getOverviewStats: mockGetOverviewStats,
    },
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
  usePathname: () => '/admin/overview',
  useSearchParams: () => new URLSearchParams(),
}));

// Must import after mock setup
import OverviewPage from '../page';

describe('OverviewPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders heading and stat cards', () => {
    mockGetOverviewStats.mockResolvedValue({
      totalGames: 0,
      publishedGames: 0,
      totalUsers: 0,
      activeUsers: 0,
      activeAiUsers: 0,
      approvalRate: 0,
      pendingApprovals: 0,
      recentSubmissions: 0,
    });

    render(<OverviewPage />);

    expect(screen.getByText('Dashboard Overview')).toBeInTheDocument();
    expect(screen.getByText('Utenti Totali')).toBeInTheDocument();
    expect(screen.getByText('Utenti Attivi (30gg)')).toBeInTheDocument();
    expect(screen.getByText('Utenti AI Attivi (30gg)')).toBeInTheDocument();
    expect(screen.getByText('Giochi')).toBeInTheDocument();
  });

  it('displays fetched stats values', async () => {
    mockGetOverviewStats.mockResolvedValue({
      totalGames: 42,
      publishedGames: 30,
      totalUsers: 150,
      activeUsers: 85,
      activeAiUsers: 23,
      approvalRate: 87.5,
      pendingApprovals: 5,
      recentSubmissions: 12,
    });

    render(<OverviewPage />);

    await waitFor(() => {
      expect(screen.getByText('150')).toBeInTheDocument();
    });

    expect(screen.getByText('85')).toBeInTheDocument();
    expect(screen.getByText('23')).toBeInTheDocument();
    expect(screen.getByText('30 / 42')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('handles API failure gracefully', async () => {
    mockGetOverviewStats.mockRejectedValue(new Error('Network error'));

    render(<OverviewPage />);

    // Should still render labels without values
    expect(screen.getByText('Utenti AI Attivi (30gg)')).toBeInTheDocument();
    expect(screen.getByText('Utenti con interazione AI')).toBeInTheDocument();
  });
});
