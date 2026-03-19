/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameTableZoneSessions } from '../GameTableZoneSessions';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ============================================================================
// Mock date-fns to avoid locale issues in CI
// ============================================================================

vi.mock('date-fns', () => ({
  formatDistanceToNow: () => '4 giorni fa',
}));

vi.mock('date-fns/locale', () => ({
  it: {},
}));

// ============================================================================
// Fixtures
// ============================================================================

const mockGameDetail: LibraryGameDetail = {
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
  timesPlayed: 42,
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
  ],
};

// ============================================================================
// Tests
// ============================================================================

describe('GameTableZoneSessions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders play stats', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.getByTestId('stat-times-played')).toHaveTextContent('42');
    expect(screen.getByTestId('stat-win-rate')).toHaveTextContent('38%');
    expect(screen.getByTestId('stat-avg-duration')).toHaveTextContent('60 min');
  });

  it('renders last played stat', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.getByTestId('stat-last-played')).toHaveTextContent('4 giorni fa');
  });

  it('renders recent session info', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g-1" />);

    expect(screen.getByTestId('session-duration')).toHaveTextContent('1h 5m');
    expect(screen.getByTestId('session-result')).toHaveTextContent('Vittoria');
    expect(screen.getByTestId('session-players')).toHaveTextContent('4 giocatori');
  });

  it('renders "Nuova sessione" button with correct link', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g-1" />);

    const btn = screen.getByTestId('new-session-btn');
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveTextContent('Nuova sessione');

    // The button is wrapped in a Link
    const link = btn.closest('a');
    expect(link).toHaveAttribute('href', '/library/games/g-1/sessions/new');
  });

  it('handles empty sessions gracefully', () => {
    const emptyDetail: LibraryGameDetail = {
      ...mockGameDetail,
      timesPlayed: 0,
      winRate: null,
      lastPlayed: null,
      avgDuration: null,
      recentSessions: [],
    };

    render(<GameTableZoneSessions gameDetail={emptyDetail} gameId="g-1" />);

    expect(screen.getByTestId('no-sessions-message')).toHaveTextContent(
      'Nessuna partita registrata'
    );
    expect(screen.getByTestId('stat-times-played')).toHaveTextContent('0');
    expect(screen.getByTestId('stat-win-rate')).toHaveTextContent('N/A');
    expect(screen.getByTestId('stat-avg-duration')).toHaveTextContent('N/A');
  });

  it('handles undefined recentSessions', () => {
    const noSessionsDetail: LibraryGameDetail = {
      ...mockGameDetail,
      recentSessions: undefined,
    };

    render(<GameTableZoneSessions gameDetail={noSessionsDetail} gameId="g-1" />);

    expect(screen.getByTestId('no-sessions-message')).toHaveTextContent(
      'Nessuna partita registrata'
    );
  });

  it('renders defeat result correctly', () => {
    const defeatDetail: LibraryGameDetail = {
      ...mockGameDetail,
      recentSessions: [
        {
          ...mockGameDetail.recentSessions![0],
          didWin: false,
        },
      ],
    };

    render(<GameTableZoneSessions gameDetail={defeatDetail} gameId="g-1" />);

    expect(screen.getByTestId('session-result')).toHaveTextContent('Sconfitta');
  });
});
