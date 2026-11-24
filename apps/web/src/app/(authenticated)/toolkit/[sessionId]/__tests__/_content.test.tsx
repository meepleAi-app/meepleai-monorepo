/**
 * Toolkit session page — #4114: a sync failure is shown next to the content, it does not replace
 * the page; and the header gets an adapted session, not a double cast that made it throw (#4113).
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ActiveSessionPageContent } from '../_content';

const useSessionSyncMock = vi.hoisted(() => vi.fn());
const storeState = vi.hoisted(() => ({ current: {} as Record<string, unknown> }));
const currentUser = vi.hoisted(() => ({ current: null as { id: string } | null }));

vi.mock('next/navigation', () => ({
  useParams: () => ({ sessionId: 'live-1' }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('sonner', () => ({ toast: { error: vi.fn(), info: vi.fn(), success: vi.fn() } }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: currentUser.current }),
}));
vi.mock('@/lib/stores/session-store', () => ({ useSessionStore: () => storeState.current }));
vi.mock('@/lib/domain-hooks/useSessionSync', () => ({
  useSessionSync: () => useSessionSyncMock(),
}));
vi.mock('@/lib/domain-hooks/useSessionToolLog', () => ({
  useSessionToolLog: () => ({ logToolAction: vi.fn() }),
}));
vi.mock('@/lib/domain-hooks/useGameToolkit', () => ({ useGameToolkit: () => ({ toolkit: null }) }));
vi.mock('@/lib/domain-hooks/useTurnOrder', () => ({
  useTurnOrder: () => ({
    turnOrder: null,
    isLoading: false,
    isAdvancing: false,
    error: null,
    advance: vi.fn(),
    reset: vi.fn(),
  }),
}));
vi.mock('@/lib/domain-hooks/useDiceRoller', () => ({ useDiceRoller: () => ({}) }));
vi.mock('@/lib/domain-hooks/useWhiteboardTool', () => ({
  useWhiteboardTool: () => ({ error: null }),
}));
vi.mock('@/components/session', () => ({
  SessionHeader: ({
    session,
    onPause,
    onFinalize,
  }: {
    session: { sessionCode: string; sessionDate: Date };
    onPause?: () => void;
    onFinalize?: () => void;
  }) => (
    // The adapter must hand over a real date: the double cast left it undefined.
    <div data-testid="session-header">
      {session.sessionCode} {session.sessionDate.toISOString()}
      {/* Like the real SessionHeader, a command shows only with its handler. */}
      {onPause && <span data-testid="header-pause" />}
      {onFinalize && <span data-testid="header-finalize" />}
    </div>
  ),
  SessionToolLayout: ({
    header,
    children,
  }: {
    header: React.ReactNode;
    children: React.ReactNode;
  }) => (
    <div>
      {header}
      {children}
    </div>
  ),
  TurnIndicatorBar: () => null,
  Scoreboard: () => null,
  CounterTool: () => null,
  DiceRoller: () => null,
  TurnOrderTool: () => null,
  WhiteboardTool: () => null,
}));

const ACTIVE_SESSION = {
  id: 'live-1',
  sessionCode: 'XYZ789',
  gameId: null,
  gameName: 'Partita libera',
  status: 'InProgress',
  createdByUserId: 'user-creator',
  createdAt: '2026-10-14T10:00:00Z',
  startedAt: '2026-10-14T10:00:00Z',
  players: [],
};

function store(overrides: Record<string, unknown> = {}) {
  return {
    activeSession: ACTIVE_SESSION,
    scores: [],
    loadSession: vi.fn().mockResolvedValue(undefined),
    pauseSession: vi.fn(),
    resumeSession: vi.fn(),
    completeSession: vi.fn(),
    handleScoreUpdate: vi.fn(),
    isLoading: false,
    error: null,
    activeTool: 'scoreboard',
    setActiveTool: vi.fn(),
    ...overrides,
  };
}

describe('Toolkit session page — #4114', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    currentUser.current = null;
    storeState.current = store();
  });

  it('keeps the session content and shows the sync banner when the stream fails', () => {
    useSessionSyncMock.mockReturnValue({
      isConnected: false,
      error: new Error('SSE connection failed'),
      scores: [],
    });

    render(<ActiveSessionPageContent />);

    expect(screen.getByTestId('session-header')).toHaveTextContent(
      'XYZ789 2026-10-14T10:00:00.000Z'
    );
    expect(screen.getByTestId('sync-unavailable-banner')).toBeInTheDocument();
    expect(screen.queryByText('SSE connection failed')).toBeNull();
  });

  it('shows no banner while the stream is fine', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: true, error: null, scores: [] });

    render(<ActiveSessionPageContent />);

    expect(screen.getByTestId('session-header')).toBeInTheDocument();
    expect(screen.queryByTestId('sync-unavailable-banner')).toBeNull();
  });

  it('still replaces the page when the session itself fails', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: false, error: null, scores: [] });
    storeState.current = store({ error: 'Session not found' });

    render(<ActiveSessionPageContent />);

    expect(screen.getByText('Session not found')).toBeInTheDocument();
    expect(screen.queryByTestId('session-header')).toBeNull();
  });

  // #4763: the server lets only the creator pause or end the game. Offering them to anyone else
  // would earn a 403, whose store error replaces this whole page.
  it('offers pause and finalize to the creator', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: true, error: null, scores: [] });
    currentUser.current = { id: 'user-creator' };

    render(<ActiveSessionPageContent />);

    expect(screen.getByTestId('header-pause')).toBeInTheDocument();
    expect(screen.getByTestId('header-finalize')).toBeInTheDocument();
  });

  it('offers neither pause nor finalize to a participant who did not create the session', () => {
    useSessionSyncMock.mockReturnValue({ isConnected: true, error: null, scores: [] });
    currentUser.current = { id: 'user-other-player' };

    render(<ActiveSessionPageContent />);

    expect(screen.getByTestId('session-header')).toBeInTheDocument();
    expect(screen.queryByTestId('header-pause')).toBeNull();
    expect(screen.queryByTestId('header-finalize')).toBeNull();
  });
});
