import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import SessionStatsPage from '../page';

const mockGetStatistics = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    sessionStatistics: {
      getStatistics: mockGetStatistics,
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

const mockData = {
  totalSessions: 42,
  totalGamesPlayed: 8,
  averageSessionDuration: '01:23:45',
  mostPlayedGames: [
    { gameId: '11111111-1111-1111-1111-111111111111', gameName: 'Catan', playCount: 15 },
    { gameId: '22222222-2222-2222-2222-222222222222', gameName: 'Ticket to Ride', playCount: 10 },
  ],
  recentScoreTrends: [
    { date: '2026-03-01', gameName: 'Catan', finalScore: 12 },
    { date: '2026-02-28', gameName: 'Ticket to Ride', finalScore: 85 },
  ],
  monthlyActivity: [
    { month: '2026-01', sessionCount: 5 },
    { month: '2026-02', sessionCount: 12 },
    { month: '2026-03', sessionCount: 3 },
  ],
};

describe('SessionStatsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders page title', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);
    expect(await screen.findByText('Session Analytics')).toBeInTheDocument();
  });

  it('shows loading spinner initially', () => {
    mockGetStatistics.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<SessionStatsPage />);
    expect(document.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('displays KPI cards with correct values', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);

    expect(await screen.findByTestId('total-sessions')).toHaveTextContent('42');
    expect(screen.getByTestId('total-games')).toHaveTextContent('8');
    expect(screen.getByTestId('avg-duration')).toHaveTextContent('01:23:45');
  });

  it('renders most played games list', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);

    const list = await screen.findByTestId('most-played-list');
    expect(within(list).getByText('Catan')).toBeInTheDocument();
    expect(within(list).getByText('15 plays')).toBeInTheDocument();
    expect(within(list).getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('renders monthly activity chart', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);

    const chart = await screen.findByTestId('monthly-chart');
    expect(within(chart).getByText('12')).toBeInTheDocument();
    expect(within(chart).getByText('01')).toBeInTheDocument();
  });

  it('renders score trends', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);

    const trends = await screen.findByTestId('score-trends');
    expect(within(trends).getByText('Catan')).toBeInTheDocument();
    expect(within(trends).getByText('85')).toBeInTheDocument();
  });

  it('shows empty state when data is null', async () => {
    mockGetStatistics.mockResolvedValue(null);
    renderWithQuery(<SessionStatsPage />);

    expect(await screen.findByText(/No session data available/)).toBeInTheDocument();
  });

  it('handles empty games list', async () => {
    mockGetStatistics.mockResolvedValue({
      ...mockData,
      mostPlayedGames: [],
      monthlyActivity: [],
      recentScoreTrends: [],
    });
    renderWithQuery(<SessionStatsPage />);

    expect(await screen.findByTestId('total-sessions')).toHaveTextContent('42');
    expect(screen.queryByTestId('most-played-list')).not.toBeInTheDocument();
  });

  it('calls API with 12 months lookback', async () => {
    mockGetStatistics.mockResolvedValue(mockData);
    renderWithQuery(<SessionStatsPage />);

    await screen.findByText('Session Analytics');
    expect(mockGetStatistics).toHaveBeenCalledWith(12);
  });
});
