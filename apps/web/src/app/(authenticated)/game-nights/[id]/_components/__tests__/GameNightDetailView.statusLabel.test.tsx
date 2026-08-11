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
// t returns the key verbatim so the resolved i18n KEY is observable in the DOM.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it-IT' }),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: getTabMock }),
}));

// Expose the statusLabel the caller computes — the real Hero renders it via a badge.
vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: ({ labels }: { labels: { statusLabel: string } }) => (
    <div data-testid="hero-status">{labels.statusLabel}</div>
  ),
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

function mockEventWithStatus(status: string) {
  detailMock.mockReturnValue({
    event: { ...baseEvent, status },
    rsvps: [{ id: 'r1', userId: 'org-1', userName: 'Marco', status: 'Accepted' }],
    actor: { actor: 'host' },
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

describe('GameNightDetailView — status i18n key (#3263 discovery)', () => {
  it('resolves the InProgress status to the camelCase key, not the toLowerCase one', () => {
    mockEventWithStatus('InProgress');
    render(<GameNightDetailView id="gn-1" />);

    // Bug: event.status.toLowerCase() -> 'inprogress', but the locale key is
    // 'inProgress' (camelCase), so the raw key leaked to the badge.
    expect(screen.getByTestId('hero-status')).toHaveTextContent(
      'gameNightDetail.status.inProgress'
    );
  });

  it('resolves the Published status to its i18n key', () => {
    mockEventWithStatus('Published');
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('hero-status')).toHaveTextContent('gameNightDetail.status.published');
  });
});
