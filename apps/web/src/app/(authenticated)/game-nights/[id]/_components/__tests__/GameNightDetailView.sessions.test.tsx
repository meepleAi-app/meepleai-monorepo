import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import type { GameNightActiveSession } from '@/stores/game-night/types';

import { GameNightDetailView } from '../GameNightDetailView';

// #4752 (the night → live game bridge): the games of the night come from the night-live read, not from the
// planning store's `activeSessions` that nothing fills. The page must keep showing them — and «Aggiungi
// partita» / «Concludi serata» — once the first game has started and the night is InProgress, not Published.

const detailMock = vi.fn();
const sessionsHookMock =
  vi.fn<(id: string, enabled: boolean) => { data?: GameNightActiveSession[] }>();

vi.mock('@/hooks/queries/useGameNightDetail', () => ({
  useGameNightDetail: () => detailMock(),
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'org-1' } }),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  usePublishGameNight: () => ({ mutate: vi.fn(), isPending: false }),
  useCancelGameNight: () => ({ mutate: vi.fn(), isPending: false }),
}));
vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: () => ({ data: { items: [] } }),
  useSharedGamesByIds: () => ({ gamesById: {}, isLoading: false }),
}));
vi.mock('@/lib/game-nights/hooks/useGameNightSessions', () => ({
  useGameNightSessions: (id: string, enabled: boolean) => sessionsHookMock(id, enabled),
}));
vi.mock('@/stores/game-night', () => ({
  useGameNightStore: () => ({
    addPlayer: vi.fn(),
    addGame: vi.fn(),
    reset: vi.fn(),
  }),
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it-IT' }),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: () => null }),
}));

vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: () => null,
  GameNightRsvpRow: () => null,
}));
vi.mock('@/components/game-night/GameNightActions', () => ({
  GameNightActions: (props: { hasActiveSession: boolean; sessionCount: number }) => (
    <div
      data-testid="game-night-actions"
      data-active={String(props.hasActiveSession)}
      data-count={String(props.sessionCount)}
    />
  ),
}));
vi.mock('@/components/game-night/GameNightSessionsList', () => ({
  GameNightSessionsList: (props: { sessions: GameNightActiveSession[] }) => (
    <ul data-testid="sessions-list">
      {props.sessions.map(s => (
        <li key={s.id}>{s.gameTitle}</li>
      ))}
    </ul>
  ),
}));
vi.mock('@/components/game-night/GameNightDiaryPanel', () => ({
  GameNightDiaryPanel: () => <div data-testid="diary-panel" />,
}));
vi.mock('@/components/game-night/planning/GameNightPlanningLayout', () => ({
  GameNightPlanningLayout: () => null,
}));
vi.mock('../GameNightEditDrawer', () => ({ GameNightEditDrawer: () => null }));
vi.mock('@/components/features/game-night-detail/voting/VotingPanel', () => ({
  VotingPanel: () => <div data-testid="voting-panel" />,
}));

type NightStatus = 'Draft' | 'Published' | 'InProgress' | 'Completed' | 'Cancelled';

const baseEvent = {
  id: 'gn-1',
  organizerId: 'org-1',
  organizerName: 'Marco',
  title: 'Serata',
  description: null,
  scheduledAt: '2026-08-01T20:00:00.000Z',
  location: null,
  maxPlayers: null,
  gameIds: [] as string[],
  acceptedCount: 1,
};

function mockDetail(status: NightStatus, actor: 'host' | 'guest' = 'host') {
  detailMock.mockReturnValue({
    event: { ...baseEvent, status },
    rsvps: [{ id: 'r1', userId: 'org-1', userName: 'Marco', status: 'Accepted' }],
    actor: { actor },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
}

function session(overrides: Partial<GameNightActiveSession> = {}): GameNightActiveSession {
  return {
    id: 's-1',
    gameNightSessionId: 's-1',
    gameId: 'g-1',
    gameTitle: 'Catan',
    playOrder: 1,
    status: 'pending',
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionsHookMock.mockReturnValue({ data: [] });
});

describe('GameNightDetailView — the games of the night (#4752)', () => {
  it('keeps the games, «Aggiungi partita» and «Concludi serata» once the first game has started (InProgress)', () => {
    mockDetail('InProgress');
    sessionsHookMock.mockReturnValue({ data: [session({ status: 'completed' })] });

    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('game-night-actions')).toBeInTheDocument();
    expect(screen.getByTestId('sessions-list')).toHaveTextContent('Catan');
    expect(screen.getByTestId('diary-panel')).toBeInTheDocument();
  });

  it('hands the games of the night-live read to the list', () => {
    mockDetail('Published');
    sessionsHookMock.mockReturnValue({
      data: [
        session({ id: 'a', gameTitle: 'Catan' }),
        session({ id: 'b', gameTitle: 'Dixit', playOrder: 2 }),
      ],
    });

    render(<GameNightDetailView id="gn-1" />);

    const list = screen.getByTestId('sessions-list');
    expect(list).toHaveTextContent('Catan');
    expect(list).toHaveTextContent('Dixit');
  });

  it('tells the actions a game is in progress, and how many games the night has', () => {
    mockDetail('InProgress');
    sessionsHookMock.mockReturnValue({
      data: [
        session({ id: 'a', status: 'completed' }),
        session({ id: 'b', status: 'in_progress', playOrder: 2 }),
      ],
    });

    render(<GameNightDetailView id="gn-1" />);

    const actions = screen.getByTestId('game-night-actions');
    expect(actions).toHaveAttribute('data-active', 'true');
    expect(actions).toHaveAttribute('data-count', '2');
  });

  it('tells the actions no game is in progress when every game is over or still to start', () => {
    mockDetail('InProgress');
    sessionsHookMock.mockReturnValue({
      data: [
        session({ id: 'a', status: 'completed' }),
        session({ id: 'b', status: 'pending', playOrder: 2 }),
      ],
    });

    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('game-night-actions')).toHaveAttribute('data-active', 'false');
  });

  it('shows an empty list, not an error, to a viewer the read refuses', () => {
    mockDetail('Published', 'guest');
    sessionsHookMock.mockReturnValue({ data: undefined });

    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('sessions-list')).toBeEmptyDOMElement();
  });

  it.each<[NightStatus, boolean]>([
    ['Published', true],
    ['InProgress', true],
    ['Completed', true],
    ['Draft', false],
    ['Cancelled', false],
  ])('reads the games of a %s night: %s', (status, enabled) => {
    mockDetail(status);

    render(<GameNightDetailView id="gn-1" />);

    expect(sessionsHookMock).toHaveBeenCalledWith('gn-1', enabled);
  });
});
