import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { CodenamesLiveFlavor } from '../CodenamesLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { generateCodenamesBoard } from '../codenames-board-preset';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1',
  sessionCode: 'ABC',
  gameId: null,
  gameName: 'Codenames',
  gameSlug: 'codenames',
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
      totalScore: 5,
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
      totalScore: 3,
      currentRank: 2,
      joinedAt: '',
      isActive: false,
    },
  ],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof CodenamesLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <CodenamesLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('CodenamesLiveFlavor', () => {
  it('renders the leaderboard even with null gameState; no board', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="codenames-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="codenames-board"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="codenames-init"]')).not.toBeNull();
  });

  it('renders the board + teams + clue when gameState is present', () => {
    const { board } = generateCodenamesBoard('red');
    useLiveSessionStore
      .getState()
      .setGameState({ v: 1, game: 'codenames', board, currentTeam: 'red', clue: null });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelectorAll('[data-slot="codenames-cell"]')).toHaveLength(25);
    expect(container.querySelector('[data-slot="codenames-teams"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="codenames-clue"]')).not.toBeNull();
  });

  it('has no axe violations (host, populated)', async () => {
    const { board } = generateCodenamesBoard('red');
    useLiveSessionStore
      .getState()
      .setGameState({
        v: 1,
        game: 'codenames',
        board,
        currentTeam: 'red',
        clue: { word: 'MARE', number: 2 },
      });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
