/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameStatsPanel } from '../GameStatsPanel';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: vi.fn(),
  libraryKeys: {
    lists: () => ['library'],
    gameDetail: (id: string) => ['library', 'game', id],
  },
}));

vi.mock('date-fns', () => ({
  format: () => '15 mar 2026',
}));

vi.mock('date-fns/locale', () => ({
  it: {},
}));

// ============================================================================
// Helpers
// ============================================================================

import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

const mockUseLibraryGameDetail = vi.mocked(useLibraryGameDetail);

const baseData = {
  libraryEntryId: 'le-1',
  userId: 'u-1',
  gameId: 'g-1',
  addedAt: '2026-01-01T00:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: null,
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: false,
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: null,
  description: null,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  complexityRating: 2.3,
  averageRating: 7.2,
  timesPlayed: 12,
  lastPlayed: '2026-03-15T18:00:00Z',
  winRate: '38%',
  avgDuration: '60 min',
  recentSessions: [
    {
      id: 's1',
      playedAt: '2026-03-15T18:00:00Z',
      durationMinutes: 65,
      durationFormatted: '1h 5m',
      didWin: true,
      players: '4',
      notes: null,
    },
    {
      id: 's2',
      playedAt: '2026-03-10T16:00:00Z',
      durationMinutes: 55,
      durationFormatted: '55m',
      didWin: false,
      players: '3',
      notes: null,
    },
  ],
};

function mockLoaded(overrides: Partial<typeof baseData> = {}) {
  mockUseLibraryGameDetail.mockReturnValue({
    data: { ...baseData, ...overrides },
    isLoading: false,
    isError: false,
    error: null,
  } as ReturnType<typeof useLibraryGameDetail>);
}

// ============================================================================
// Tests
// ============================================================================

describe('GameStatsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders 4 stat cards with correct values', () => {
    mockLoaded();
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('stat-times-played')).toHaveTextContent('12');
    expect(screen.getByTestId('stat-win-rate')).toHaveTextContent('38%');
    expect(screen.getByTestId('stat-last-played')).toHaveTextContent('15 mar 2026');
    expect(screen.getByTestId('stat-avg-duration')).toHaveTextContent('60 min');
  });

  it('renders session history rows', () => {
    mockLoaded();
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('session-history')).toBeInTheDocument();
    expect(screen.getByTestId('session-row-s1')).toBeInTheDocument();
    expect(screen.getByTestId('session-row-s2')).toBeInTheDocument();
  });

  it('shows win/loss badges correctly in session rows', () => {
    mockLoaded();
    render(<GameStatsPanel gameId="g-1" />);

    const s1 = screen.getByTestId('session-row-s1');
    expect(s1).toHaveTextContent('Vittoria');

    const s2 = screen.getByTestId('session-row-s2');
    expect(s2).toHaveTextContent('Sconfitta');
  });

  it('shows empty state when timesPlayed is 0', () => {
    mockLoaded({ timesPlayed: 0, recentSessions: [] });
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
    expect(screen.getByText('Nessuna partita registrata')).toBeInTheDocument();
    expect(screen.queryByTestId('stats-grid')).not.toBeInTheDocument();
  });

  it('shows empty state when data is null', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      error: null,
    } as ReturnType<typeof useLibraryGameDetail>);

    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as ReturnType<typeof useLibraryGameDetail>);

    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('stats-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('stats-grid')).not.toBeInTheDocument();
    expect(screen.queryByTestId('empty-state')).not.toBeInTheDocument();
  });

  it('uses dash fallback for null winRate and avgDuration', () => {
    mockLoaded({ winRate: null, avgDuration: null });
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('stat-win-rate')).toHaveTextContent('—');
    expect(screen.getByTestId('stat-avg-duration')).toHaveTextContent('—');
  });

  it('uses dash fallback for null lastPlayed', () => {
    mockLoaded({ lastPlayed: null });
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.getByTestId('stat-last-played')).toHaveTextContent('—');
  });

  it('does not render session history when recentSessions is empty', () => {
    mockLoaded({ recentSessions: [] });
    render(<GameStatsPanel gameId="g-1" />);

    expect(screen.queryByTestId('session-history')).not.toBeInTheDocument();
    expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
  });
});
