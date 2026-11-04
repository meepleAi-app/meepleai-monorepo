/**
 * ScoreTabContent — the Score tab on `RoundScores` (#4156 fetta 2, ADR-089).
 *
 * Executable examples of the issue: a score on the current round, a correction, a score on a
 * future round. Plus the derived round (D6), who may write, and what is never written.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { ScoreTabContent } from '../ScoreTabContent';

const toastErrorMock = vi.hoisted(() => vi.fn());
vi.mock('sonner', () => ({ toast: { error: toastErrorMock } }));

const recordMock = vi.hoisted(() => vi.fn());
vi.mock('@/hooks/queries/useRecordRoundScore', () => ({
  useRecordRoundScore: () => ({ mutateAsync: recordMock, isPending: false }),
}));

const MESSAGES: Record<string, string> = {
  'pages.sessionLive.scoring.loadingLabel': 'Caricamento punteggi…',
  'pages.sessionLive.scoring.rounds.roundLabel': 'Round {round}',
  'pages.sessionLive.scoring.rounds.previousRound': 'Round precedente',
  'pages.sessionLive.scoring.rounds.nextRound': 'Round successivo',
  'pages.sessionLive.scoring.rounds.currentRound': 'Torna al round in corso',
  'pages.sessionLive.scoring.rounds.player': 'Giocatore',
  'pages.sessionLive.scoring.rounds.total': 'Totale',
  'pages.sessionLive.scoring.rounds.noDimensions': 'Nessuna dimensione',
  'pages.sessionLive.scoring.rounds.noPlayers': 'Nessun giocatore',
  'pages.sessionLive.scoring.rounds.cellAriaTemplate': '{dimension} di {player}, round {round}',
  'pages.sessionLive.scoring.rounds.saveError': 'Punteggio non salvato',
  'pages.sessionLive.scoring.rounds.notAnInteger': 'Numero intero',
};

function player(id: string, name: string, totalScore = 0, overrides: Record<string, unknown> = {}) {
  return {
    id,
    userId: null,
    displayName: name,
    avatarUrl: null,
    color: 'Red',
    role: 'Player',
    teamId: null,
    totalScore,
    currentRank: 1,
    joinedAt: '2026-10-14T10:00:00Z',
    isActive: true,
    ...overrides,
  };
}

function session(overrides: Record<string, unknown> = {}): LiveSessionDto {
  return {
    id: 'live-1',
    status: 'InProgress',
    // 2 players, turn 3 → derived round 2.
    currentTurnIndex: 3,
    players: [player('marco', 'Marco', 10), player('giulia', 'Giulia', 0)],
    roundScores: [
      { playerId: 'marco', round: 2, dimension: 'punti', value: 10, unit: null, recordedAt: '' },
    ],
    scoringConfig: { enabledDimensions: ['punti'], dimensionUnits: {} },
    ...overrides,
  } as unknown as LiveSessionDto;
}

function renderTab(
  live: LiveSessionDto | null | undefined,
  viewerRole: 'Host' | 'Player' | 'Spectator' = 'Host'
): ReturnType<typeof render> {
  const ui: ReactElement = (
    <IntlProvider locale="it" messages={MESSAGES}>
      <ScoreTabContent sessionId="live-1" session={live} viewerRole={viewerRole} />
    </IntlProvider>
  );
  return render(ui);
}

function write(label: string, value: string) {
  const cell = screen.getByLabelText(label);
  fireEvent.change(cell, { target: { value } });
  fireEvent.blur(cell);
}

describe('ScoreTabContent — #4156 fetta 2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    recordMock.mockResolvedValue(undefined);
  });

  describe('what the tab shows', () => {
    it('waits for the live session', () => {
      renderTab(undefined);
      expect(screen.getByRole('status')).toHaveTextContent('Caricamento punteggi…');
    });

    it('says so when the game has no scoring dimension, or nobody scores', () => {
      const { unmount } = renderTab(
        session({ scoringConfig: { enabledDimensions: [], dimensionUnits: {} } })
      );
      expect(screen.getByRole('status')).toHaveTextContent('Nessuna dimensione');
      unmount();
      renderTab(session({ players: [player('ospite', 'Ospite', 0, { role: 'Spectator' })] }));
      expect(screen.getByRole('status')).toHaveTextContent('Nessun giocatore');
    });

    it('lists the scoring players with the cells of the round and the backend totals', () => {
      renderTab(
        session({
          players: [
            player('marco', 'Marco', 10),
            player('giulia', 'Giulia', 0),
            player('ospite', 'Ospite', 0, { role: 'Spectator' }),
          ],
        }),
        'Player'
      );
      const rows = screen.getAllByRole('row').slice(1);
      expect(rows.map(row => row.querySelector('th')?.textContent)).toEqual(['Marco', 'Giulia']);
      expect(rows[0]).toHaveTextContent('10');
      expect(rows[1]).toHaveTextContent('—');
    });

    it('shows the dimension unit in the column header', () => {
      renderTab(
        session({
          scoringConfig: { enabledDimensions: ['oro'], dimensionUnits: { oro: 'monete' } },
        })
      );
      expect(screen.getByRole('columnheader', { name: 'oro (monete)' })).toBeInTheDocument();
    });
  });

  describe('the round — derived (D6), changeable', () => {
    it('opens on the derived round of the game', () => {
      renderTab(session({ currentTurnIndex: 5 })); // 2 players → (5 − 1) / 2 + 1 = 3
      expect(screen.getByText('Round 3')).toBeInTheDocument();
    });

    it('is round 1 before the start', () => {
      renderTab(session({ currentTurnIndex: 0 }));
      expect(screen.getByText('Round 1')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Round precedente' })).toBeDisabled();
    });

    it('moves to another round and back to the current one', () => {
      renderTab(session());
      fireEvent.click(screen.getByRole('button', { name: 'Round successivo' }));
      expect(screen.getByText('Round 3')).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Torna al round in corso' }));
      expect(screen.getByText('Round 2')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Torna al round in corso' })).toBeNull();
    });
  });

  describe('who writes', () => {
    it('the other players read, without inputs', () => {
      renderTab(session(), 'Player');
      expect(screen.queryByRole('spinbutton')).toBeNull();
    });

    it('nobody writes before the start or after the end', () => {
      const { unmount } = renderTab(session({ status: 'Created' }));
      expect(screen.queryByRole('spinbutton')).toBeNull();
      unmount();
      renderTab(session({ status: 'Completed' }));
      expect(screen.queryByRole('spinbutton')).toBeNull();
    });

    it('the host writes while the game is paused too', () => {
      renderTab(session({ status: 'Paused' }));
      expect(screen.getAllByRole('spinbutton').length).toBeGreaterThan(0);
    });
  });

  describe('executable examples', () => {
    it('a score on the current round is a new cell (POST)', async () => {
      renderTab(session());
      write('punti di Giulia, round 2', '30');
      await waitFor(() =>
        expect(recordMock).toHaveBeenCalledWith({
          request: { playerId: 'giulia', round: 2, dimension: 'punti', value: 30 },
          correction: false,
        })
      );
    });

    it('a correction rewrites a cell that has a value (PUT)', async () => {
      renderTab(session());
      write('punti di Marco, round 2', '12');
      await waitFor(() =>
        expect(recordMock).toHaveBeenCalledWith({
          request: { playerId: 'marco', round: 2, dimension: 'punti', value: 12 },
          correction: true,
        })
      );
    });

    it('a score on a future round is a new cell of that round', async () => {
      renderTab(session());
      fireEvent.click(screen.getByRole('button', { name: 'Round successivo' }));
      write('punti di Marco, round 3', '5');
      await waitFor(() =>
        expect(recordMock).toHaveBeenCalledWith({
          request: { playerId: 'marco', round: 3, dimension: 'punti', value: 5 },
          correction: false,
        })
      );
    });

    it('sends the unit of the dimension', async () => {
      renderTab(
        session({
          scoringConfig: { enabledDimensions: ['oro'], dimensionUnits: { oro: 'monete' } },
        })
      );
      write('oro di Giulia, round 2', '7');
      await waitFor(() =>
        expect(recordMock).toHaveBeenCalledWith({
          request: { playerId: 'giulia', round: 2, dimension: 'oro', value: 7, unit: 'monete' },
          correction: false,
        })
      );
    });

    it('Enter writes the cell', async () => {
      renderTab(session());
      const cell = screen.getByLabelText('punti di Giulia, round 2');
      cell.focus();
      fireEvent.change(cell, { target: { value: '4' } });
      fireEvent.keyDown(cell, { key: 'Enter' });
      await waitFor(() => expect(recordMock).toHaveBeenCalledTimes(1));
    });
  });

  describe('what is never written', () => {
    it('an unchanged or emptied cell', async () => {
      renderTab(session());
      write('punti di Marco, round 2', '10');
      write('punti di Giulia, round 2', '');
      // Give the write queue a turn: nothing must have been sent.
      await Promise.resolve();
      expect(recordMock).not.toHaveBeenCalled();
    });

    it('a value that is not a whole number, with a message', async () => {
      renderTab(session());
      write('punti di Giulia, round 2', '2.5');
      await Promise.resolve();
      expect(recordMock).not.toHaveBeenCalled();
      expect(toastErrorMock).toHaveBeenCalledWith('Numero intero', expect.anything());
    });
  });

  describe('writes', () => {
    it('go one at a time: the second starts when the first is done', async () => {
      let finishFirst: () => void = () => undefined;
      recordMock.mockImplementationOnce(
        () => new Promise<void>(resolve => (finishFirst = resolve))
      );
      renderTab(session());
      write('punti di Giulia, round 2', '30');
      write('punti di Marco, round 2', '12');
      await waitFor(() => expect(recordMock).toHaveBeenCalledTimes(1));
      finishFirst();
      await waitFor(() => expect(recordMock).toHaveBeenCalledTimes(2));
    });

    it('a failed write says so', async () => {
      recordMock.mockRejectedValueOnce(new Error('409'));
      renderTab(session());
      write('punti di Giulia, round 2', '30');
      await waitFor(() =>
        expect(toastErrorMock).toHaveBeenCalledWith('Punteggio non salvato', expect.anything())
      );
    });
  });
});
