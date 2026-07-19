import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { CatanLiveFlavor } from '../CatanLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { generateStandardBoard } from '../catan-board-preset';
import { emptyCatanPlayerState } from '../catan-state';

expect.extend(toHaveNoViolations);

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

// #2788: CatanLiveFlavor now self-builds its labels via useTranslation/useIntl,
// so tests must supply the IntlProvider. `onError` swallows react-intl
// MISSING_TRANSLATION noise (empty messages → t() returns the raw key).
const CTA_KEY = 'pages.sessionLive.flavor.catan.initBoardCta';
const WAITING_KEY = 'pages.sessionLive.flavor.catan.viewerWaiting';

function renderFlavor(props: Parameters<typeof CatanLiveFlavor>[0]) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <CatanLiveFlavor {...props} />
    </IntlProvider>
  );
}

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
    renderFlavor({ session, viewerRole: 'Host', sessionId: 's1' });
    expect(screen.getByRole('button', { name: CTA_KEY })).toBeInTheDocument();
  });

  it('empty state — non-host sees the waiting message, no CTA', () => {
    renderFlavor({ session, viewerRole: 'Player', sessionId: 's1' });
    expect(screen.queryByRole('button', { name: CTA_KEY })).toBeNull();
    expect(screen.getByText(WAITING_KEY)).toBeInTheDocument();
  });

  it('populated — renders board + dice + one card per player', () => {
    useLiveSessionStore.getState().setGameState({
      v: 1,
      game: 'catan',
      board: generateStandardBoard(),
      dice: { last: 8, history: [8] },
      players: { p1: emptyCatanPlayerState(), p2: emptyCatanPlayerState() },
    });
    const { container } = renderFlavor({ session, viewerRole: 'Player', sessionId: 's1' });
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
    const { container } = renderFlavor({ session, viewerRole: 'Host', sessionId: 's1' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
