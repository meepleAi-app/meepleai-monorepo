/**
 * PlayPanel — #4156 (D4): an active session in Setup or Paused opens the live page of the
 * LiveGameSession correlated to the GameSession. Without one there is no live page: the card
 * opens the session detail, as it does for the other states.
 */

import { fireEvent, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { PlayPanel } from '../PlayPanel';

const routerPush = vi.hoisted(() => vi.fn());
const openDetail = vi.hoisted(() => vi.fn());
const useActiveSessionsMock = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: routerPush }) }));
vi.mock('@/hooks/useNavigation', () => ({ useNavigation: () => ({ openDetail }) }));
vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => useActiveSessionsMock(),
  sessionsKeys: { history: (params: unknown) => ['sessions', 'history', params] },
}));
vi.mock('@/lib/api', () => ({
  api: { sessions: { getHistory: vi.fn().mockResolvedValue({ sessions: [] }) } },
}));
vi.mock('@/components/features/common', () => ({
  EmptyStateCard: () => null,
  SkeletonCardGrid: () => null,
}));
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: { title: string; onClick?: () => void }) => (
    <button type="button" onClick={props.onClick}>
      {props.title}
    </button>
  ),
  entityHsl: () => 'hsl(0 0% 0%)',
}));

function session(id: string, liveSessionId: string | null) {
  return {
    id,
    liveSessionId,
    gameId: 'game-1',
    status: 'Paused',
    startedAt: '2026-04-20T19:00:00Z',
    completedAt: null,
    playerCount: 2,
    players: [],
    winnerName: null,
    notes: `notes ${id}`,
    durationMinutes: 30,
  };
}

describe('PlayPanel — #4156 (D4)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useActiveSessionsMock.mockReturnValue({
      data: { sessions: [session('gs-1', 'live-1'), session('gs-2', null)] },
      isLoading: false,
    });
  });

  it('opens the live page of the correlated live session', () => {
    renderWithQuery(<PlayPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'notes gs-1' }));
    expect(routerPush).toHaveBeenCalledWith('/sessions/live-1/live');
    expect(openDetail).not.toHaveBeenCalled();
  });

  it('opens the session detail when there is no live session', () => {
    renderWithQuery(<PlayPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'notes gs-2' }));
    expect(routerPush).not.toHaveBeenCalled();
    expect(openDetail).toHaveBeenCalledWith('gs-2', 'session');
  });
});
