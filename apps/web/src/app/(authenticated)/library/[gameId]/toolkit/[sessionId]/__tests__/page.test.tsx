/**
 * Library toolkit session page — #4114: a sync failure is shown next to the content, it does not
 * replace the page. Before, any SSE error replaced the whole page with «Error · SSE connection
 * failed», though the session's data had loaded.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SessionPage from '../page';

const useSessionSyncMock = vi.hoisted(() => vi.fn());
const storeState = vi.hoisted(() => ({ current: {} as Record<string, unknown> }));

const routerPush = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: () => ({ gameId: 'game-1', sessionId: 'live-1' }),
  useRouter: () => ({ push: routerPush }),
}));
vi.mock('sonner', () => ({ toast: { error: vi.fn(), info: vi.fn(), success: vi.fn() } }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({ useCurrentUser: () => ({ data: null }) }));
vi.mock('@/lib/domain-hooks/useSessionSync', () => ({
  useSessionSync: () => useSessionSyncMock(),
}));
vi.mock('@/lib/stores/session-store', () => ({ useSessionStore: () => storeState.current }));
vi.mock('@/components/session', () => ({
  SessionHeader: ({
    session,
    onFinalize,
  }: {
    session: { sessionCode: string };
    onFinalize?: () => void;
  }) => (
    <div data-testid="session-header">
      {session.sessionCode}
      <button type="button" onClick={onFinalize}>
        Finalize
      </button>
    </div>
  ),
  MeepleParticipantCard: () => null,
  ScoreInput: () => null,
  Scoreboard: () => null,
}));

const ACTIVE_SESSION = {
  id: 'live-1',
  sessionCode: 'ABC123',
  gameId: 'game-1',
  gameName: 'Azul',
  status: 'InProgress',
  createdAt: '2026-10-14T10:00:00Z',
  startedAt: '2026-10-14T10:00:00Z',
  players: [],
};

function store(overrides: Record<string, unknown> = {}) {
  return {
    activeSession: ACTIVE_SESSION,
    scores: [],
    loadSession: vi.fn().mockResolvedValue(undefined),
    recordScore: vi.fn(),
    pauseSession: vi.fn(),
    resumeSession: vi.fn(),
    completeSession: vi.fn(),
    handleScoreUpdate: vi.fn(),
    isLoading: false,
    error: null,
    ...overrides,
  };
}

describe('Library toolkit session page — #4114', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    storeState.current = store();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));
  });

  it('keeps the session content and shows the sync banner when the stream fails', () => {
    useSessionSyncMock.mockReturnValue({
      isConnected: false,
      error: new Error('SSE connection failed'),
      scores: [],
    });

    render(<SessionPage />);

    expect(screen.getByTestId('session-header')).toHaveTextContent('ABC123');
    expect(screen.getByTestId('sync-unavailable-banner')).toBeInTheDocument();
    expect(screen.queryByText('SSE connection failed')).toBeNull();
  });

  it('shows no banner while the stream is fine', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: true, error: null, scores: [] });

    render(<SessionPage />);

    expect(screen.getByTestId('session-header')).toBeInTheDocument();
    expect(screen.queryByTestId('sync-unavailable-banner')).toBeNull();
  });

  it('still replaces the page when the session itself fails', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: false, error: null, scores: [] });
    storeState.current = store({ error: 'Session not found' });

    render(<SessionPage />);

    expect(screen.getByText('Session not found')).toBeInTheDocument();
    expect(screen.queryByTestId('session-header')).toBeNull();
  });

  it('finalize goes back to the game without waiting for the stream', async () => {
    useSessionSyncMock.mockReturnValue({
      isConnected: false,
      error: new Error('SSE connection failed'),
      scores: [],
    });
    const completeSession = vi.fn().mockResolvedValue(undefined);
    storeState.current = store({ completeSession });

    render(<SessionPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Finalize' }));

    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/library/game-1'));
    expect(completeSession).toHaveBeenCalledTimes(1);
  });
});
