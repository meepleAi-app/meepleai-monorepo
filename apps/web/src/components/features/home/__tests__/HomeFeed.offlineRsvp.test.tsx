import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { HomeFeed } from '../HomeFeed';

// #2989 gap 5: the pending-RSVP card on the mobile home dashboard is offline-disabled
// (it can't reach the server), driven by useNetworkStatus, and re-enables on reconnect.

const networkStatusMock = vi.fn<() => { isOffline: boolean }>();
const upcomingNightsMock = vi.fn();

vi.mock('@/hooks/useNetworkStatus', () => ({
  useNetworkStatus: () => networkStatusMock(),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  useRsvpGameNight: () => ({ mutate: vi.fn(), isPending: false, variables: undefined }),
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
});
