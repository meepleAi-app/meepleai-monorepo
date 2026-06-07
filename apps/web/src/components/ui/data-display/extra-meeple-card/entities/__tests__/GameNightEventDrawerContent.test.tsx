import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { GameNightEventDrawerContent } from '../GameNightEventDrawerContent';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock useGameNight + useGameNightRsvps hooks
vi.mock('@/hooks/queries/useGameNights', () => ({
  useGameNight: vi.fn(),
  useGameNightRsvps: vi.fn(),
}));

// Mock cascade-navigation-store
const pushDrawerMock = vi.fn();
vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: (selector: (s: { pushDrawer: typeof pushDrawerMock }) => unknown) =>
    selector({ pushDrawer: pushDrawerMock }),
}));

import { useGameNight, useGameNightRsvps } from '@/hooks/queries/useGameNights';

const mockGameNight = {
  id: 'gn-1',
  organizerId: 'u-org',
  organizerName: 'Anna Host',
  title: 'Serata Catan',
  description: 'Partita epica',
  scheduledAt: '2026-08-15T19:30:00Z',
  location: 'Milano',
  maxPlayers: 6,
  gameIds: [],
  status: 'Published' as const,
  acceptedCount: 2,
  pendingCount: 1,
  totalInvited: 3,
  createdAt: '2026-08-01T10:00:00Z',
};

const mockRsvps = [
  {
    id: 'r1',
    userId: 'player-uuid-1',
    userName: 'Mario Rossi',
    status: 'Accepted' as const,
    respondedAt: null,
    createdAt: '2026-08-01T10:00:00Z',
  },
  {
    id: 'r2',
    userId: 'player-uuid-2',
    userName: 'Luigi Bianchi',
    status: 'Pending' as const,
    respondedAt: null,
    createdAt: '2026-08-01T11:00:00Z',
  },
];

describe('GameNightEventDrawerContent (#1929 WP3)', () => {
  beforeEach(() => {
    vi.mocked(useGameNight).mockReturnValue({
      data: mockGameNight,
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useGameNight>);
    vi.mocked(useGameNightRsvps).mockReturnValue({
      data: mockRsvps,
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useGameNightRsvps>);
    pushDrawerMock.mockClear();
  });

  it('renders loading skeleton when loading', () => {
    vi.mocked(useGameNight).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as ReturnType<typeof useGameNight>);
    vi.mocked(useGameNightRsvps).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as ReturnType<typeof useGameNightRsvps>);
    render(<GameNightEventDrawerContent entityId="gn-1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('renders game night title and overview tab with date', () => {
    render(<GameNightEventDrawerContent entityId="gn-1" />);
    expect(screen.getByText('Serata Catan')).toBeInTheDocument();
    expect(screen.getByText('Data e ora')).toBeInTheDocument();
  });

  it('renders Giocatori tab with RSVP list and correct testids', async () => {
    const user = userEvent.setup();
    render(<GameNightEventDrawerContent entityId="gn-1" />);
    await user.click(screen.getByRole('tab', { name: /giocatori/i }));
    expect(screen.getByTestId('gamenight-player-player-uuid-1')).toBeInTheDocument();
    expect(screen.getByTestId('gamenight-player-player-uuid-2')).toBeInTheDocument();
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
    expect(screen.getByText('Luigi Bianchi')).toBeInTheDocument();
  });

  it('clicking player button calls pushDrawer with player userId', async () => {
    const user = userEvent.setup();
    render(<GameNightEventDrawerContent entityId="gn-1" />);
    await user.click(screen.getByRole('tab', { name: /giocatori/i }));
    await user.click(screen.getByTestId('gamenight-player-player-uuid-1'));
    expect(pushDrawerMock).toHaveBeenCalledWith('player', 'player-uuid-1');
  });

  it('renders error state when gameNight not found', () => {
    vi.mocked(useGameNight).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as ReturnType<typeof useGameNight>);
    render(<GameNightEventDrawerContent entityId="gn-1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });
});
