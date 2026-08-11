import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { GameNightDetailView } from '../GameNightDetailView';

// #2989 Screen C mobile parity: on <768px the guest RSVP action bar and the
// host "Invia inviti" (Draft) CTA are lifted into a thumb-reachable sticky
// bottom bar that clears the fixed MobileBottomBar (bottom-16), collapsing back
// to inline flow at md+. This removes the decision-3c deferral documented in
// GameNightDetailView (the previous inline placement).

const detailMock = vi.fn();
const getTabMock = vi.fn<(key: string) => string | null>();
const currentUserMock = vi.fn();
const networkStatusMock = vi.fn<() => { isOffline: boolean }>();

vi.mock('@/hooks/queries/useGameNightDetail', () => ({
  useGameNightDetail: () => detailMock(),
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => currentUserMock(),
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
// #2989 gap 5: RSVP CTAs are offline-disabled from useNetworkStatus.
vi.mock('@/hooks/useNetworkStatus', () => ({
  useNetworkStatus: () => networkStatusMock(),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: getTabMock }),
}));

vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: (props: { disabled?: boolean }) => (
    <div data-testid="rsvp-action-bar" data-disabled={String(Boolean(props.disabled))} />
  ),
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

function mockDetail(overrides: Record<string, unknown>) {
  detailMock.mockReturnValue({
    event: { ...baseEvent, status: 'Published' },
    rsvps: [],
    actor: { actor: 'guest' },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
    ...overrides,
  });
}

// A mobile sticky bar is `position: fixed` above the MobileBottomBar on small
// screens and collapses to normal flow at md+.
function expectMobileSticky(el: HTMLElement) {
  expect(el.className).toContain('fixed');
  expect(el.className).toContain('bottom-16');
  expect(el.className).toContain('md:static');
}

beforeEach(() => {
  vi.clearAllMocks();
  getTabMock.mockReturnValue(null);
  currentUserMock.mockReturnValue({ data: { id: 'guest-1' } });
  networkStatusMock.mockReturnValue({ isOffline: false });
});

describe('GameNightDetailView — mobile sticky action bars (#2989 Screen C)', () => {
  it('wraps the guest RSVP action bar in a mobile sticky bottom bar on a Published night', () => {
    mockDetail({ actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    const bar = screen.getByTestId('rsvp-sticky-bar');
    expectMobileSticky(bar);
    // The action bar itself lives inside the sticky wrapper.
    expect(bar.querySelector('[data-testid="rsvp-action-bar"]')).not.toBeNull();
  });

  // #2989 gap 5: while offline the guest RSVP CTAs must be disabled (they can't
  // reach the server) and re-enable on reconnect.
  it('disables the guest RSVP action bar while offline', () => {
    networkStatusMock.mockReturnValue({ isOffline: true });
    mockDetail({ actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('rsvp-action-bar').getAttribute('data-disabled')).toBe('true');
  });

  it('leaves the guest RSVP action bar enabled while online', () => {
    networkStatusMock.mockReturnValue({ isOffline: false });
    mockDetail({ actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.getByTestId('rsvp-action-bar').getAttribute('data-disabled')).toBe('false');
  });

  it('wraps the host "Invia inviti" CTA in a mobile sticky bottom bar on a Draft night', () => {
    currentUserMock.mockReturnValue({ data: { id: 'org-1' } });
    mockDetail({ event: { ...baseEvent, status: 'Draft' }, actor: { actor: 'host' } });
    render(<GameNightDetailView id="gn-1" />);

    const bar = screen.getByTestId('host-sticky-bar');
    expectMobileSticky(bar);
    // The publish CTA lives inside the sticky wrapper.
    expect(bar.querySelector('[data-testid="publish-game-night"]')).not.toBeNull();
  });

  it('does NOT make the destructive Cancel a sticky bar on a Published host view', () => {
    currentUserMock.mockReturnValue({ data: { id: 'org-1' } });
    mockDetail({ event: { ...baseEvent, status: 'Published' }, actor: { actor: 'host' } });
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.queryByTestId('host-sticky-bar')).not.toBeInTheDocument();
  });

  // Gap 4: the fixed sticky bar overlays the bottom of the scroll region, so
  // the container carries the clearance classes below md. NOTE: jsdom does not
  // compute layout, so these assert the *class contract* (not pixel geometry):
  // `pb-40` (base) + `sm:pb-40` (the latter specifically guards the 640–767px
  // band, where the inherited `sm:py-8` from PADDING_DEFAULT would otherwise
  // clobber the reservation to 32px while the bar is still fixed) + `md:pb-4`.
  it('applies the sticky-bar clearance classes when a guest RSVP bar is shown (Published)', () => {
    mockDetail({ actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    const container = screen.getByTestId('game-night-detail-container');
    expect(container.className).toContain('pb-40');
    expect(container.className).toContain('sm:pb-40');
    expect(container.className).toContain('md:pb-4');
  });

  it('applies the sticky-bar clearance classes on a Draft host night too', () => {
    currentUserMock.mockReturnValue({ data: { id: 'org-1' } });
    mockDetail({ event: { ...baseEvent, status: 'Draft' }, actor: { actor: 'host' } });
    render(<GameNightDetailView id="gn-1" />);

    const container = screen.getByTestId('game-night-detail-container');
    expect(container.className).toContain('pb-40');
    expect(container.className).toContain('sm:pb-40');
  });

  it('does NOT apply the clearance when no sticky bar is shown (Completed)', () => {
    mockDetail({ event: { ...baseEvent, status: 'Completed' }, actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    const container = screen.getByTestId('game-night-detail-container');
    expect(container.className).not.toContain('pb-40');
  });

  // Gap (F3): the voting tab (showDetailsContent === false) must NOT render the
  // RSVP sticky bar or reserve its clearance — otherwise the bar would overlap
  // the voting UI. Guards against a regression that drops `&& showDetailsContent`.
  it('does NOT render the RSVP sticky bar or clearance on the voting tab (guest Published)', () => {
    getTabMock.mockReturnValue('voting');
    mockDetail({ actor: { actor: 'guest' } });
    render(<GameNightDetailView id="gn-1" />);

    expect(screen.queryByTestId('rsvp-sticky-bar')).not.toBeInTheDocument();
    const container = screen.getByTestId('game-night-detail-container');
    expect(container.className).not.toContain('pb-40');
  });
});
