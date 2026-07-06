import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

// ── Hook mocks ────────────────────────────────────────────────────────────────
const detailMock = vi.fn();
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
  useSearchParams: () => ({ get: () => null }),
}));

// Stub heavy children so the test focuses on the view's edit wiring.
vi.mock('@/components/game-night/planning/GameNightPlanningLayout', () => ({
  GameNightPlanningLayout: () => <div data-testid="planning-layout" />,
}));
vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: () => null,
  GameNightRsvpRow: () => null,
}));
// Stub the edit drawer to assert it is mounted with the right props.
vi.mock('../GameNightEditDrawer', () => ({
  GameNightEditDrawer: (props: { gameNightId: string; organizerId: string }) => (
    <div
      data-testid="edit-drawer"
      data-game-night-id={props.gameNightId}
      data-organizer-id={props.organizerId}
    />
  ),
}));

const draftEvent = {
  id: 'gn-1',
  status: 'Draft' as const,
  organizerId: 'org-1',
  organizerName: 'Marco',
  title: 'Serata Catan',
  description: 'Snack inclusi',
  scheduledAt: '2026-08-01T20:00:00.000Z',
  location: 'Casa di Marco',
  maxPlayers: 6,
  gameIds: [] as string[],
  acceptedCount: 0,
};

beforeEach(() => {
  vi.clearAllMocks();
  detailMock.mockReturnValue({
    event: draftEvent,
    rsvps: [],
    actor: { actor: 'host' },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
});

describe('GameNightDetailView — edit drawer wiring (ADR-079 / #2701)', () => {
  it('routes the Draft host edit control to the ?action=edit deep-link (not the legacy /edit page)', () => {
    render(<GameNightDetailView id="gn-1" />);

    const editLink = screen.getByRole('link', { name: /gameNightDetail\.actor\.host\.edit/ });
    expect(editLink).toHaveAttribute('href', '/game-nights/gn-1?action=edit');
  });

  it('mounts GameNightEditDrawer with the event id + organizer id', () => {
    render(<GameNightDetailView id="gn-1" />);

    const drawer = screen.getByTestId('edit-drawer');
    expect(drawer).toHaveAttribute('data-game-night-id', 'gn-1');
    expect(drawer).toHaveAttribute('data-organizer-id', 'org-1');
  });
});
