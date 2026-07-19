import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { HomeFeed } from '../HomeFeed';

// #2989 gap 5: the pending-RSVP card on the mobile home dashboard is offline-disabled
// (it can't reach the server), driven by useNetworkStatus, and re-enables on reconnect.

const networkStatusMock = vi.fn<() => { isOffline: boolean }>();
const upcomingNightsMock = vi.fn();
const rsvpMutationMock = vi.fn();

vi.mock('@/hooks/useNetworkStatus', () => ({
  useNetworkStatus: () => networkStatusMock(),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  useRsvpGameNight: () => rsvpMutationMock(),
  useUpcomingGameNights: () => upcomingNightsMock(),
}));
vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => ({ data: { sessions: [] }, isLoading: false }),
}));
vi.mock('@/hooks/queries/useLibrary', () => ({
  useRecentlyAddedGames: () => ({ data: { items: [] }, isLoading: false }),
}));
vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: () => ({ data: { sessions: [] }, isLoading: false }),
}));
vi.mock('@/hooks/useNavigation', () => ({ useNavigation: () => ({ openDetail: vi.fn() }) }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

vi.mock('@/components/game-night/PendingRsvpCard', () => ({
  PendingRsvpCard: (props: { disabled?: boolean }) => (
    <div data-testid="pending-rsvp-card" data-disabled={String(Boolean(props.disabled))} />
  ),
}));
// Stub the rest of the dashboard surface so the test is scoped to the RSVP card.
vi.mock('@/components/chat-unified/MeepleChatCard', () => ({ MeepleChatCard: () => null }));
vi.mock('@/components/features/common', () => ({
  EmptyStateCard: () => null,
  SkeletonCardGrid: () => null,
}));
vi.mock('@/components/game-night/MeepleEventCard', () => ({ MeepleEventCard: () => null }));
vi.mock('@/components/library/MeepleLibraryGameCard', () => ({
  MeepleLibraryGameCard: () => null,
}));
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: () => null,
  entityHsl: () => 'hsl(0 0% 0%)',
}));
vi.mock('../RecentSection', () => ({ RecentSection: () => null }));

const pendingNight = {
  id: 'gn-1',
  title: 'Serata',
  organizerName: 'Marco',
  viewerRsvpStatus: 'Pending',
};

beforeEach(() => {
  vi.clearAllMocks();
  networkStatusMock.mockReturnValue({ isOffline: false });
  upcomingNightsMock.mockReturnValue({ data: [pendingNight], isLoading: false });
  rsvpMutationMock.mockReturnValue({ mutate: vi.fn(), isPending: false, variables: undefined });
});

describe('HomeFeed — offline-disabled pending RSVP (#2989 gap 5)', () => {
  it('disables the pending-RSVP card while offline', () => {
    networkStatusMock.mockReturnValue({ isOffline: true });
    render(<HomeFeed />);

    expect(screen.getByTestId('pending-rsvp-card').getAttribute('data-disabled')).toBe('true');
  });

  it('leaves the pending-RSVP card enabled while online', () => {
    networkStatusMock.mockReturnValue({ isOffline: false });
    render(<HomeFeed />);

    expect(screen.getByTestId('pending-rsvp-card').getAttribute('data-disabled')).toBe('false');
  });

  // Guards the other half of `isOffline || (isPending && variables?.id === night.id)`:
  // an in-flight RSVP for THIS night disables its card even while online, so the
  // double-submit guard survives a regression to the correlation check.
  it('disables the pending-RSVP card while its own RSVP mutation is in flight (online)', () => {
    networkStatusMock.mockReturnValue({ isOffline: false });
    rsvpMutationMock.mockReturnValue({
      mutate: vi.fn(),
      isPending: true,
      variables: { id: 'gn-1' },
    });
    render(<HomeFeed />);

    expect(screen.getByTestId('pending-rsvp-card').getAttribute('data-disabled')).toBe('true');
  });

  // Correlation guard: an in-flight RSVP for a DIFFERENT night must NOT disable this card.
  it("keeps the card enabled when a different night's RSVP is in flight (online)", () => {
    networkStatusMock.mockReturnValue({ isOffline: false });
    rsvpMutationMock.mockReturnValue({
      mutate: vi.fn(),
      isPending: true,
      variables: { id: 'other-night' },
    });
    render(<HomeFeed />);

    expect(screen.getByTestId('pending-rsvp-card').getAttribute('data-disabled')).toBe('false');
  });
});
