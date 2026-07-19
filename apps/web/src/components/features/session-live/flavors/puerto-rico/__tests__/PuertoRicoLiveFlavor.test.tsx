import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { PuertoRicoLiveFlavor } from '../PuertoRicoLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { initialPuertoRicoState } from '../puerto-rico-state';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1',
  sessionCode: 'ABC',
  gameId: null,
  gameName: 'Puerto Rico',
  gameSlug: 'puerto-rico',
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
      totalScore: 5,
      currentRank: 2,
      joinedAt: '',
      isActive: false,
    },
  ],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof PuertoRicoLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <PuertoRicoLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('PuertoRicoLiveFlavor', () => {
  it('renders the leaderboard even with null gameState; no panels', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="pr-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="pr-galleons"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pr-init"]')).not.toBeNull();
  });

  it('renders the panels + a mat per player when gameState is present', () => {
    useLiveSessionStore.getState().setGameState(initialPuertoRicoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="pr-galleons"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pr-trading"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="pr-colonist-ship"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="pr-player-mat"]')).toHaveLength(2);
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore.getState().setGameState(initialPuertoRicoState(['p1', 'p2']));
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
