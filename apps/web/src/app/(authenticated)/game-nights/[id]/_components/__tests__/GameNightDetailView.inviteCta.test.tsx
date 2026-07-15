import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

// #2963 / invariant #16 (tagged vs invited): a Draft night shows *tagged*
// players (no invite sent) with the "Invia inviti" host CTA; publishing turns
// Pending rows into *invited, awaiting reply*.

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
  // Expose the resolved status props so the tagged-vs-invited copy is assertable.
  GameNightRsvpRow: (props: { status: string; statusLabel: string }) => (
    <div data-testid="rsvp-row" data-status={props.status} data-status-label={props.statusLabel} />
  ),
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

function mockDetail(status: 'Draft' | 'Published') {
  detailMock.mockReturnValue({
    event: { ...baseEvent, status },
    rsvps: roster,
    actor: { actor: 'host' },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
}

function pendingRowLabel(): string | null {
  const row = screen
    .getAllByTestId('rsvp-row')
    .find(el => el.getAttribute('data-status') === 'Pending');
  return row?.getAttribute('data-status-label') ?? null;
}

beforeEach(() => {
  vi.clearAllMocks();
  getTabMock.mockReturnValue(null);
});

describe('GameNightDetailView — Invia inviti / tagged vs invited (#2963, invariant #16)', () => {
  it('renders the "Invia inviti" host CTA on a Draft night', () => {
    mockDetail('Draft');
    render(<GameNightDetailView id="gn-1" />);
    const cta = screen.getByTestId('publish-game-night');
    // t() returns the key in the test harness — the button resolves the publish
    // label key, whose value is "Invia inviti" (it) / "Send invites" (en).
    expect(cta).toHaveTextContent('gameNightDetail.actor.host.publish');
  });

  it('labels Pending roster rows as *tagged* (no invite sent) on a Draft night', () => {
    mockDetail('Draft');
    render(<GameNightDetailView id="gn-1" />);
    expect(pendingRowLabel()).toBe('gameNightDetail.participants.rsvpStatus.tagged');
  });

  it('labels Pending roster rows as *invited/pending* once Published', () => {
    mockDetail('Published');
    render(<GameNightDetailView id="gn-1" />);
    expect(pendingRowLabel()).toBe('gameNightDetail.participants.rsvpStatus.pending');
    // The publish CTA is gone once the night is Published (invites already sent).
    expect(screen.queryByTestId('publish-game-night')).not.toBeInTheDocument();
  });
});
