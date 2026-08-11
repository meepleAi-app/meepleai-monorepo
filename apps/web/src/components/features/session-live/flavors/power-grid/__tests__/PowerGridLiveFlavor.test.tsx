import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { PowerGridLiveFlavor } from '../PowerGridLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialPowerGridState } from '../power-grid-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1',
  sessionCode: 'ABC',
  gameId: null,
  gameName: 'Power Grid',
  gameSlug: 'power-grid',
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
      totalScore: 6,
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

function renderFlavor(props: Partial<Parameters<typeof PowerGridLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <PowerGridLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('PowerGridLiveFlavor', () => {
  it('renders the leaderboard with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="pg-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="pg-plants"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pg-init"]')).not.toBeNull();
  });

  it('renders plant + resource panels when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialPowerGridState());
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pg-plants"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pg-resources"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="pg-plant-slot"]')).toHaveLength(8);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialPowerGridState());
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
