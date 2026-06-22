import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';

import { TurnIndicatorRenderer } from '../TurnIndicatorRenderer';
import type { TurnIndicatorRendererLabels } from '../labels';
import type { TurnState, PlayerInfo } from '@/lib/session-live/turn-state';

const PLAYERS: PlayerInfo[] = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Sara' },
];

const LABELS: TurnIndicatorRendererLabels = {
  roundRobinHeading: 'Round-robin',
  sequentialHeading: 'Fasi',
  simultaneousHeading: 'Simultaneo',
  realtimeHeading: 'Tempo reale',
  noneHeading: 'Libero',
  customHeading: 'Custom',
  firstPlayerTokenHeading: 'Token primo giocatore',
  unknownTitle: 'Tipo di turno non supportato',
  unknownBody: "Aggiorna l'app per supportare questa modalità.",
  yourTurnLabel: 'Tuo turno',
  waitingLabel: 'In attesa',
  roundCountTemplate: 'Round {current} di {total}',
  playOrderHeading: 'Ordine di gioco',
  firstPlayerTokenHolderTemplate: 'Token: {playerName}',
};

function renderRenderer(state: TurnState) {
  return render(
    <IntlProvider locale="it" messages={{}}>
      <TurnIndicatorRenderer state={state} players={PLAYERS} viewerId="p1" labels={LABELS} />
    </IntlProvider>
  );
}

describe('TurnIndicatorRenderer dispatch', () => {
  let warnSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    warnSpy.mockRestore();
  });

  it.each([
    [
      'RoundRobin',
      {
        type: 'RoundRobin',
        round: 1,
        totalRounds: 4,
        activePlayerId: 'p1',
        playOrder: ['p1', 'p2'],
      } as TurnState,
      'turn-branch-round-robin',
    ],
    [
      'Sequential',
      {
        type: 'Sequential',
        phases: ['Mattina', 'Notte'],
        activePhaseIndex: 0,
      } as TurnState,
      'turn-branch-sequential',
    ],
    ['Simultaneous', { type: 'Simultaneous' } as TurnState, 'turn-branch-simultaneous'],
    ['Realtime', { type: 'Realtime' } as TurnState, 'turn-branch-realtime'],
    ['None', { type: 'None' } as TurnState, 'turn-branch-none'],
    [
      'Custom',
      { type: 'Custom', phases: ['F1'], activePhaseIndex: 0 } as TurnState,
      'turn-branch-custom',
    ],
    [
      'FirstPlayerToken',
      {
        type: 'FirstPlayerToken',
        round: 1,
        totalRounds: 4,
        tokenHolderId: 'p1',
        playOrder: ['p1', 'p2'],
      } as TurnState,
      'turn-branch-first-player-token',
    ],
  ])('renders %s branch', (_name, state, slot) => {
    const { container } = renderRenderer(state as TurnState);
    expect(container.querySelector(`[data-slot="${slot}"]`)).not.toBeNull();
    expect(container.querySelector('[data-slot="turn-indicator"]')).not.toBeNull();
  });

  it('renders Unknown branch when type is not registered + warns', () => {
    const { container } = renderRenderer({
      type: 'BogusType',
    } as unknown as TurnState);
    expect(container.querySelector('[data-slot="turn-branch-unknown"]')).not.toBeNull();
    expect(warnSpy).toHaveBeenCalledWith(
      '[TurnIndicatorRenderer] Unknown turnOrderType:',
      'BogusType'
    );
  });

  it('renders RoundRobin gracefully when activePlayerId is not in players', () => {
    const { container } = renderRenderer({
      type: 'RoundRobin',
      round: 1,
      totalRounds: 4,
      activePlayerId: 'ghost',
      playOrder: ['ghost'],
    });
    expect(container.querySelector('[data-slot="turn-branch-round-robin"]')).not.toBeNull();
    expect(screen.getByText(/Sconosciuto/)).toBeInTheDocument();
  });

  it('renders Sequential gracefully when phases is empty', () => {
    const { container } = renderRenderer({
      type: 'Sequential',
      phases: [],
      activePhaseIndex: 0,
    });
    expect(container.querySelector('[data-slot="turn-branch-sequential"]')).not.toBeNull();
    expect(screen.getByText(/Nessuna fase configurata/)).toBeInTheDocument();
  });
});
