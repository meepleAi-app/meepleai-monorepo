import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

// #4706: a guest invited by email lives apart from the member RSVPs, so the organizer saw "Partecipanti (1)"
// with only the registered members, before and after the guest accepted. The host's roster now lists them too.

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
  useTranslation: () => ({
    t: (k: string, p?: { count?: number }) => (p?.count !== undefined ? `${k}:${p.count}` : k),
    locale: 'it-IT',
  }),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: getTabMock }),
}));

vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: () => null,
  GameNightRsvpRow: (props: {
    userName: string;
    status: string;
    statusLabel: string;
    subtitle?: string;
  }) => (
    <div
      data-testid="row"
      data-status={props.status}
      data-label={props.statusLabel}
      data-subtitle={props.subtitle ?? ''}
    >
      {props.userName}
    </div>
  ),
}));
vi.mock('@/components/game-night/GameNightActions', () => ({
  GameNightActions: () => null,
}));
vi.mock('@/components/game-night/GameNightSessionsList', () => ({
  GameNightSessionsList: () => null,
}));
vi.mock('@/components/game-night/GameNightDiaryPanel', () => ({
  GameNightDiaryPanel: () => null,
}));
vi.mock('@/components/game-night/planning/GameNightPlanningLayout', () => ({
  GameNightPlanningLayout: () => null,
}));
vi.mock('../GameNightEditDrawer', () => ({ GameNightEditDrawer: () => null }));
vi.mock('@/components/features/game-night-detail/voting/VotingPanel', () => ({
  VotingPanel: () => null,
}));

const baseEvent = {
  id: 'gn-1',
  organizerId: 'org-1',
  organizerName: 'Anna',
  title: 'Serata Catan',
  description: null,
  scheduledAt: '2026-08-01T20:00:00.000Z',
  location: null,
  maxPlayers: null,
  gameIds: [] as string[],
  acceptedCount: 0,
  status: 'Published',
};

const roster = [
  { id: 'r1', userId: 'org-1', userName: 'Anna', status: 'Accepted' },
  { id: 'r2', userId: 'u-2', userName: 'Bruno', status: 'Pending' },
];

const guest = (over: Record<string, unknown>) => ({
  id: 'g-1',
  email: 'carla@example.com',
  status: 'Pending',
  respondedAt: null,
  respondedByName: null,
  expiresAt: '2026-11-05T10:00:00Z',
  createdAt: '2026-10-29T10:00:00Z',
  ...over,
});

function mockDetail(actor: 'host' | 'guest', guestInvitations: unknown) {
  detailMock.mockReturnValue({
    event: baseEvent,
    rsvps: roster,
    guestInvitations,
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

describe('GameNightDetailView — guests invited by email', () => {
  it('lists a guest still waiting by their address, and counts them with the members', () => {
    mockDetail('host', [guest({})]);
    render(<GameNightDetailView id="gn-1" />);

    expect(
      screen.getByRole('heading', { name: 'gameNightDetail.participants.sectionTitle:3' })
    ).toBeInTheDocument();
    const row = screen.getByText('carla@example.com');
    expect(row).toHaveAttribute('data-status', 'Pending');
    expect(row).toHaveAttribute('data-label', 'gameNightDetail.participants.rsvpStatus.pending');
    expect(row).toHaveAttribute('data-subtitle', 'gameNightDetail.participants.guestByEmail');
  });

  it('shows an accepted guest under the name they left, with the address as the second line', () => {
    mockDetail('host', [
      guest({
        id: 'g-2',
        email: 'dario@example.com',
        status: 'Accepted',
        respondedByName: ' Dario V. ',
      }),
    ]);
    render(<GameNightDetailView id="gn-1" />);

    const row = screen.getByText('Dario V.');
    expect(row).toHaveAttribute('data-status', 'Accepted');
    expect(row).toHaveAttribute('data-label', 'gameNightDetail.participants.rsvpStatus.accepted');
    expect(row).toHaveAttribute(
      'data-subtitle',
      'dario@example.com · gameNightDetail.participants.guestByEmail'
    );
  });

  it('draws a declined guest as declined and an expired one as waiting but labelled expired', () => {
    mockDetail('host', [
      guest({ id: 'g-3', email: 'elena@example.com', status: 'Declined' }),
      guest({ id: 'g-4', email: 'franco@example.com', status: 'Expired' }),
    ]);
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByText('elena@example.com')).toHaveAttribute('data-status', 'Declined');
    const expired = screen.getByText('franco@example.com');
    expect(expired).toHaveAttribute('data-status', 'Pending');
    expect(expired).toHaveAttribute(
      'data-label',
      'gameNightDetail.participants.rsvpStatus.expired'
    );
  });

  it('leaves out an invitation the organizer withdrew', () => {
    mockDetail('host', [guest({ id: 'g-5', email: 'revocato@example.com', status: 'Cancelled' })]);
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.queryByText('revocato@example.com')).not.toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'gameNightDetail.participants.sectionTitle:2' })
    ).toBeInTheDocument();
  });

  it('keeps the members first and the guests after them', () => {
    mockDetail('host', [guest({})]);
    render(<GameNightDetailView id="gn-1" />);

    const rows = screen.getAllByTestId('row').map(r => r.textContent);
    expect(rows).toEqual(['Anna', 'Bruno', 'carla@example.com']);
  });

  it('shows only the members to a participant, who is not given the guests', () => {
    mockDetail('guest', undefined);
    render(<GameNightDetailView id="gn-1" />);

    const list = screen.getByRole('list');
    expect(within(list).getAllByTestId('row')).toHaveLength(2);
    expect(
      screen.getByRole('heading', { name: 'gameNightDetail.participants.sectionTitle:2' })
    ).toBeInTheDocument();
  });
});
