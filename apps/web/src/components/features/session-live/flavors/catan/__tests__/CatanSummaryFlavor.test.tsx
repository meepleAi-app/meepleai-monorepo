import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { CatanSummaryFlavor } from '../CatanSummaryFlavor';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

const MESSAGES = {
  'pages.sessionSummary.flavor.catan.winnerTemplate': '{name} vince!',
  'pages.sessionSummary.flavor.catan.vpUnit': 'PV',
  'pages.sessionSummary.flavor.catan.durationTemplate': '{minutes} min',
  'pages.sessionSummary.flavor.catan.standingsTitle': 'Classifica finale',
  'pages.sessionSummary.flavor.catan.empty': 'Riepilogo non disponibile',
};

const renderWithIntl = (ui: ReactElement) =>
  render(
    <IntlProvider locale="it" messages={MESSAGES} onError={() => {}}>
      {ui}
    </IntlProvider>
  );

const base: GameSessionDto = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T00:47:00Z',
  playerCount: 2,
  players: [],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 47,
  scoringType: 'Points',
  scoreData: JSON.stringify({
    scores: [
      { playerId: 'p1', points: 10 },
      { playerId: 'p2', points: 8 },
    ],
  }),
  gameSlug: 'catan',
  gameName: 'Catan',
  scorePlayers: [
    { id: 'p1', displayName: 'Alice', color: 'Red' },
    { id: 'p2', displayName: 'Bob', color: 'Blue' },
  ],
};

describe('CatanSummaryFlavor', () => {
  it('renders winner hero (winnerName) + ordered standings', () => {
    renderWithIntl(<CatanSummaryFlavor session={base} />);
    expect(screen.getByText('Alice vince!')).toBeInTheDocument();
    expect(screen.getAllByTestId('catan-summary-row-name').map(n => n.textContent)).toEqual([
      'Alice',
      'Bob',
    ]);
  });

  it('does not auto-crown when no isWinner and winnerName null', () => {
    const noWinner: GameSessionDto = {
      ...base,
      winnerName: null,
      scoreData: JSON.stringify({
        scores: [
          { playerId: 'p1', points: 0 },
          { playerId: 'p2', points: 0 },
        ],
      }),
    };
    renderWithIntl(<CatanSummaryFlavor session={noWinner} />);
    expect(screen.queryByText(/vince!/)).toBeNull();
    expect(screen.getAllByTestId('catan-summary-row-name').length).toBe(2);
  });

  it('renders empty state when no scorePlayers', () => {
    renderWithIntl(
      <CatanSummaryFlavor session={{ ...base, scorePlayers: null, scoreData: null }} />
    );
    expect(screen.getByText('Riepilogo non disponibile')).toBeInTheDocument();
  });
});
