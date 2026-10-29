import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

// "Aggiungi partita" and "Concludi serata" (GameNightActions) are organizer-only on the server: starting a session
// answers 403 "Only the organizer can start sessions." and completing the night 403 "Only the night organizer can
// complete it." An invited guest used to see both buttons on a live night and met the 403 only after clicking.

const detailMock = vi.fn();
const getTabMock = vi.fn<(key: string) => string | null>();

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
vi.mock('@/stores/game-night', () => ({
  useGameNightStore: () => ({
    addPlayer: vi.fn(),
    addGame: vi.fn(),
    reset: vi.fn(),
    activeSessions: [],
  }),
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it-IT' }),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: getTabMock }),
}));

vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: () => null,
  GameNightRsvpRow: () => null,
}));
vi.mock('@/components/game-night/GameNightActions', () => ({
  GameNightActions: () => <div data-testid="game-night-actions" />,
}));
vi.mock('@/components/game-night/GameNightSessionsList', () => ({
  GameNightSessionsList: () => <div data-testid="sessions-list" />,
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

const roster = [
  { id: 'r1', userId: 'org-1', userName: 'Marco', status: 'Accepted' },
  { id: 'r2', userId: 'guest-1', userName: 'Davide', status: 'Pending' },
];

function mockDetail(status: 'Published' | 'Completed', actor: 'host' | 'guest') {
  detailMock.mockReturnValue({
    event: { ...baseEvent, status },
    rsvps: roster,
    actor: { actor },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  getTabMock.mockReturnValue(null);
});

describe('GameNightDetailView — organizer-only session actions', () => {
  it('shows "Aggiungi partita" / "Concludi serata" to the host on a live night', () => {
    mockDetail('Published', 'host');
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.getByTestId('game-night-actions')).toBeInTheDocument();
  });

  it('shows them to the host on a completed night too (unchanged)', () => {
    mockDetail('Completed', 'host');
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.getByTestId('game-night-actions')).toBeInTheDocument();
  });

  it('hides them from an invited guest on a live night, but keeps the sessions list and the diary', () => {
    mockDetail('Published', 'guest');
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.queryByTestId('game-night-actions')).not.toBeInTheDocument();
    expect(screen.getByTestId('sessions-list')).toBeInTheDocument();
    expect(screen.getByTestId('diary-panel')).toBeInTheDocument();
  });

  it('hides them from a guest on a completed night', () => {
    mockDetail('Completed', 'guest');
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.queryByTestId('game-night-actions')).not.toBeInTheDocument();
  });
});
