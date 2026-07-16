import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { WingspanLiveFlavor } from '../WingspanLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

expect.extend(toHaveNoViolations);

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1',
  sessionCode: 'ABC',
  gameId: null,
  gameName: 'Wingspan',
  gameSlug: 'wingspan',
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
      totalScore: 12,
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
      totalScore: 9,
      currentRank: 2,
      joinedAt: '',
      isActive: false,
    },
  ],
  teams: [],
  roundScores: [
    { playerId: 'p1', round: 1, dimension: 'eggs', value: 3, unit: null, recordedAt: '' },
  ],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof WingspanLiveFlavor>[0]> = {}) {
  return render(
    // onError swallows react-intl MISSING_TRANSLATION noise (empty messages → t() returns the key).
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <WingspanLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}

beforeEach(() => useLiveSessionStore.getState().reset());

describe('WingspanLiveFlavor', () => {
  it('renders the leaderboard + category breakdown even with null gameState', () => {
    const { container } = renderFlavor();
    expect(container.querySelector('[data-slot="wingspan-breakdown"]')).not.toBeNull();
    expect(container.querySelectorAll('[data-slot="wingspan-leaderboard-row"]')).toHaveLength(2);
    // no round tracker (gameState null) — but for a host, a CTA appears
    expect(container.querySelector('[data-slot="wingspan-round-tracker"]')).toBeNull();
  });

  it('host sees the init-round CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="wingspan-round-init"]')).not.toBeNull();
  });

  it('renders the round tracker when gameState is present', () => {
    useLiveSessionStore
      .getState()
      .setGameState({ v: 1, game: 'wingspan', round: 2, roundGoals: [] });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="wingspan-round-tracker"]')).not.toBeNull();
  });

  it('has no axe violations (host, populated)', async () => {
    useLiveSessionStore
      .getState()
      .setGameState({ v: 1, game: 'wingspan', round: 2, roundGoals: [{ label: 'Nidi' }] });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
