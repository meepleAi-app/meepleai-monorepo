import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

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
  GameNightRsvpRow: () => <div data-testid="rsvp-row" />,
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

const publishedEvent = {
  id: 'gn-1',
  status: 'Published' as const,
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

beforeEach(() => {
  vi.clearAllMocks();
  getTabMock.mockReturnValue(null);
  detailMock.mockReturnValue({
    event: publishedEvent,
    rsvps: [{ id: 'r1', userId: 'org-1', userName: 'Marco', status: 'Accepted' }],
    actor: { actor: 'host' },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
});

describe('GameNightDetailView — voting tab (#2723)', () => {
  it('renders the Dettagli/Votazione tab strip on a Published event', () => {
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.getByTestId('tab-details')).toHaveAttribute('href', '/game-nights/gn-1');
    expect(screen.getByTestId('tab-voting')).toHaveAttribute(
      'href',
      '/game-nights/gn-1?tab=voting'
    );
  });

  it('shows the details body (roster) and hides the voting panel by default', () => {
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.getByTestId('rsvp-row')).toBeInTheDocument();
    expect(screen.queryByTestId('voting-panel')).not.toBeInTheDocument();
  });

  it('shows the voting panel and hides the details body when ?tab=voting', () => {
    getTabMock.mockImplementation(k => (k === 'tab' ? 'voting' : null));
    render(<GameNightDetailView id="gn-1" />);
    expect(screen.getByTestId('voting-panel')).toBeInTheDocument();
    expect(screen.queryByTestId('rsvp-row')).not.toBeInTheDocument();
    expect(screen.queryByTestId('sessions-list')).not.toBeInTheDocument();
  });
});
