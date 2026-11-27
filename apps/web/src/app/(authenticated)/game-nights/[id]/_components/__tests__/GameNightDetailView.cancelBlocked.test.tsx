import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ApiError } from '@/lib/api/core/errors';

import { GameNightDetailView } from '../GameNightDetailView';

// #4780 (decision of 2026-11-25): a night is not cancelled while one of its games is being played. The server
// answers 409 `game_night_has_live_game`, and the organizer is told to end the game first — not the generic error.

const detailMock = vi.fn();
const cancelMutate = vi.fn();
const toastMock = vi.fn();

vi.mock('@/hooks/queries/useGameNightDetail', () => ({
  useGameNightDetail: () => detailMock(),
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'org-1' } }),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  usePublishGameNight: () => ({ mutate: vi.fn(), isPending: false }),
  useCancelGameNight: () => ({ mutate: cancelMutate, isPending: false }),
}));
vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: () => ({ data: { items: [] } }),
  useSharedGamesByIds: () => ({ gamesById: {}, isLoading: false }),
}));
vi.mock('@/lib/game-nights/hooks/useGameNightSessions', () => ({
  useGameNightSessions: () => ({ data: [] }),
}));
vi.mock('@/stores/game-night', () => ({
  useGameNightStore: () => ({ addPlayer: vi.fn(), addGame: vi.fn(), reset: vi.fn() }),
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: toastMock }) }));
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

beforeEach(() => {
  vi.clearAllMocks();
  detailMock.mockReturnValue({
    event: {
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
      status: 'Published',
    },
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

function refuseWith(error: unknown) {
  cancelMutate.mockImplementation((_id: string, options?: { onError?: (e: unknown) => void }) =>
    options?.onError?.(error)
  );
}

describe('GameNightDetailView — a cancellation refused under a game being played (#4780)', () => {
  it('tells the organizer to end the game first', async () => {
    refuseWith(
      new ApiError({
        message: 'A game of this night is being played: end it before cancelling the night.',
        statusCode: 409,
        code: 'game_night_has_live_game',
      })
    );
    render(<GameNightDetailView id="gn-1" />);

    await userEvent.click(
      screen.getByRole('button', { name: 'gameNightDetail.actor.host.cancel' })
    );

    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ description: 'gameNightDetail.actor.host.cancelBlockedByLiveGame' })
    );
  });

  it('keeps the generic error for any other refusal', async () => {
    refuseWith(new ApiError({ message: 'conflict', statusCode: 409, code: 'conflict' }));
    render(<GameNightDetailView id="gn-1" />);

    await userEvent.click(
      screen.getByRole('button', { name: 'gameNightDetail.actor.host.cancel' })
    );

    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ description: 'gameNightDetail.rsvp.errors.generic' })
    );
  });
});
