import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { CatanLiveFlavor, type CatanLiveFlavorLabels } from '../CatanLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { generateStandardBoard } from '../catan-board-preset';
import { emptyCatanPlayerState } from '../catan-state';

expect.extend(toHaveNoViolations);

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const labels: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  phaseTemplate: 'Fase: {name}',
  initBoardCta: 'Genera board Catan',
  viewerWaiting: 'In attesa dell’host',
  hexAriaTemplate: '{terrain} {number}',
  robberLabel: 'Ladro',
  diceLastLabel: 'Ultimo tiro',
  diceHistoryLabel: 'Cronologia',
  rollAriaTemplate: 'Registra tiro {n}',
  vpLabel: 'PV',
  handLabel: 'Mano',
  devLabel: 'Sviluppo',
  settlementsLabel: 'Insediamenti',
  citiesLabel: 'Città',
  roadsLabel: 'Strade',
  longestRoadLabel: 'Strada+',
  largestArmyLabel: 'Armata+',
  incAriaTemplate: '{field} +1',
  decAriaTemplate: '{field} -1',
};

const session = {
  id: 's1',
  sessionCode: 'ABC',
  gameId: null,
  gameName: 'Catan',
  gameSlug: 'catan',
  createdByUserId: 'u1',
  status: 'InProgress',
  visibility: 'Private',
  groupId: null,
  createdAt: '',
  startedAt: '',
  pausedAt: null,
  completedAt: null,
  updatedAt: '',
  lastSavedAt: null,
  currentTurnIndex: 0,
  currentTurnPlayerId: 'p1',
  agentMode: 'None',
  notes: null,
  players: [
    {
      id: 'p1',
      userId: null,
      displayName: 'Marco',
      avatarUrl: null,
      color: 'Red',
      role: 'Host',
      teamId: null,
      totalScore: 8,
      currentRank: 1,
      joinedAt: '',
      isActive: true,
    },
    {
      id: 'p2',
      userId: null,
      displayName: 'Anna',
      avatarUrl: null,
      color: 'Blue',
      role: 'Player',
      teamId: null,
      totalScore: 7,
      currentRank: 2,
      joinedAt: '',
      isActive: false,
    },
  ],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

beforeEach(() => useLiveSessionStore.getState().reset());

describe('CatanLiveFlavor', () => {
  it('empty state — host sees the "Genera board" CTA', () => {
    render(<CatanLiveFlavor session={session} labels={labels} viewerRole="Host" sessionId="s1" />);
    expect(screen.getByRole('button', { name: 'Genera board Catan' })).toBeInTheDocument();
  });

  it('empty state — non-host sees the waiting message, no CTA', () => {
    render(
      <CatanLiveFlavor session={session} labels={labels} viewerRole="Player" sessionId="s1" />
    );
    expect(screen.queryByRole('button', { name: 'Genera board Catan' })).toBeNull();
    expect(screen.getByText('In attesa dell’host')).toBeInTheDocument();
  });

  it('populated — renders board + dice + one card per player', () => {
    useLiveSessionStore.getState().setGameState({
      v: 1,
      game: 'catan',
      board: generateStandardBoard(),
      dice: { last: 8, history: [8] },
      players: { p1: emptyCatanPlayerState(), p2: emptyCatanPlayerState() },
    });
    const { container } = render(
      <CatanLiveFlavor session={session} labels={labels} viewerRole="Player" sessionId="s1" />
    );
    expect(container.querySelector('[data-slot="catan-board"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="catan-dice"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="catan-player-card"]')).toHaveLength(2);
  });

  it('has no axe violations in the populated host view', async () => {
    useLiveSessionStore.getState().setGameState({
      v: 1,
      game: 'catan',
      board: generateStandardBoard(),
      dice: { last: 8, history: [8] },
      players: { p1: emptyCatanPlayerState(), p2: emptyCatanPlayerState() },
    });
    const { container } = render(
      <CatanLiveFlavor session={session} labels={labels} viewerRole="Host" sessionId="s1" />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
