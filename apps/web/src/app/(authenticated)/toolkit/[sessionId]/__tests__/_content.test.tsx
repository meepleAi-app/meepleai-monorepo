/**
 * Toolkit session page — #4114: a sync failure is shown next to the content, it does not replace
 * the page; and the header gets an adapted session, not a double cast that made it throw (#4113).
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ActiveSessionPageContent } from '../_content';

const useSessionSyncMock = vi.hoisted(() => vi.fn());
const storeState = vi.hoisted(() => ({ current: {} as Record<string, unknown> }));

vi.mock('next/navigation', () => ({
  useParams: () => ({ sessionId: 'live-1' }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('sonner', () => ({ toast: { error: vi.fn(), info: vi.fn(), success: vi.fn() } }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({ useCurrentUser: () => ({ data: null }) }));
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
  SessionHeader: ({ session }: { session: { sessionCode: string; sessionDate: Date } }) => (
    // The adapter must hand over a real date: the double cast left it undefined.
    <div data-testid="session-header">
      {session.sessionCode} {session.sessionDate.toISOString()}
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
});
