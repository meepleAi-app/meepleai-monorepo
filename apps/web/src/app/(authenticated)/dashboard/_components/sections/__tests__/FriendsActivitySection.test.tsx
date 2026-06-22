/**
 * FriendsActivitySection — priority #4 dashboard slot (Asse C plan v2 WP5 T5).
 *
 * Covers:
 *   1. Default state renders activity entries
 *   2. Renders avatar buttons (aria-label per friend)
 *   3. Click on avatar opens cascade drawer for 'player'
 *   4. Empty state renders EmptySection
 *   5. Loading state renders 3 skeletons
 *   6. Error state returns null (silent)
 *   7. Default + activities=[] returns null (silent fallback)
 *   8. Verb i18n: "completed" → "ha completato"
 *   9. Verb i18n: "created" → "ha creato"
 *  10. Verb i18n: "joined" → "si è unito a"
 *  11. Routing: game refs link to /games/:id, gameNight refs link to /game-nights/:id
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const openDrawerMock = vi.fn();

vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: (selector: (s: { openDrawer: typeof openDrawerMock }) => unknown) =>
    selector({ openDrawer: openDrawerMock }),
}));

vi.mock('next/link', () => ({
  __esModule: true,
  default: ({
    href,
    children,
    ...props
  }: React.PropsWithChildren<{ href: string } & Record<string, unknown>>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

import {
  FriendsActivitySection,
  type FriendsActivitySectionProps,
} from '../FriendsActivitySection';
import type { FriendActivity } from '@/hooks/use-friends-activity';

const NOW = new Date();
const ONE_MIN_AGO = new Date(NOW.getTime() - 60_000).toISOString();
const ONE_HOUR_AGO = new Date(NOW.getTime() - 3_600_000).toISOString();

const baseActivities: readonly FriendActivity[] = [
  {
    friendUserId: 'friend-1',
    avatar: 'https://example.com/avatar1.jpg',
    name: 'Marco',
    verb: 'completed',
    gameOrEventId: 'game-1',
    gameOrEventType: 'game',
    gameOrEventName: 'Catan',
    timestamp: ONE_MIN_AGO,
  },
  {
    friendUserId: 'friend-2',
    avatar: 'https://example.com/avatar2.jpg',
    name: 'Anna',
    verb: 'created',
    gameOrEventId: 'event-1',
    gameOrEventType: 'gameNight',
    gameOrEventName: 'Serata Boardgame',
    timestamp: ONE_HOUR_AGO,
  },
];

function renderSection(props: Partial<FriendsActivitySectionProps> = {}) {
  return render(
    <FriendsActivitySection
      state={props.state ?? 'default'}
      activities={props.activities ?? baseActivities}
    />
  );
}

describe('FriendsActivitySection', () => {
  beforeEach(() => {
    openDrawerMock.mockClear();
  });

  it('renders activity entries in default state', () => {
    renderSection();
    expect(screen.getByTestId('friends-activity-list')).toBeInTheDocument();
    expect(screen.getByTestId('friends-entry-friend-1')).toBeInTheDocument();
    expect(screen.getByTestId('friends-entry-friend-2')).toBeInTheDocument();
  });

  it('renders avatar buttons with aria-label per friend', () => {
    renderSection();
    const avatar1 = screen.getByTestId('friends-avatar-friend-1');
    const avatar2 = screen.getByTestId('friends-avatar-friend-2');
    expect(avatar1).toHaveAttribute('aria-label', 'Apri profilo di Marco');
    expect(avatar2).toHaveAttribute('aria-label', 'Apri profilo di Anna');
  });

  it('opens cascade drawer with player entity when avatar clicked', async () => {
    const { default: userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    renderSection();
    await user.click(screen.getByTestId('friends-avatar-friend-1'));
    expect(openDrawerMock).toHaveBeenCalledWith('player', 'friend-1');
  });

  it('renders empty state with EmptySection', () => {
    renderSection({ state: 'empty', activities: undefined });
    expect(screen.getByTestId('friends-empty')).toBeInTheDocument();
    expect(screen.getByText(/nessuna attività recente/i)).toBeInTheDocument();
  });

  it('renders 3 skeleton entries in loading state', () => {
    renderSection({ state: 'loading', activities: undefined });
    expect(screen.getByTestId('friends-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('friends-activity-list')).not.toBeInTheDocument();
  });

  it('returns null in error state (silent)', () => {
    const { container } = renderSection({ state: 'error', activities: undefined });
    expect(container.firstChild).toBeNull();
  });

  it('returns null in default state when activities array is empty (silent fallback)', () => {
    const { container } = renderSection({ state: 'default', activities: [] });
    expect(container.firstChild).toBeNull();
  });

  it('maps verb "completed" to italian "ha completato"', () => {
    renderSection();
    expect(screen.getByText('ha completato')).toBeInTheDocument();
  });

  it('maps verb "created" to italian "ha creato"', () => {
    renderSection();
    expect(screen.getByText('ha creato')).toBeInTheDocument();
  });

  it('maps verb "joined" to italian "si è unito a"', () => {
    const joinedActivity: FriendActivity = {
      friendUserId: 'friend-3',
      avatar: '',
      name: 'Luca',
      verb: 'joined',
      gameOrEventId: 'event-2',
      gameOrEventType: 'gameNight',
      gameOrEventName: 'Torneo Risk',
      timestamp: ONE_MIN_AGO,
    };
    renderSection({ activities: [joinedActivity] });
    expect(screen.getByText('si è unito a')).toBeInTheDocument();
  });

  it('routes game-type refs to /games/:id and gameNight-type refs to /game-nights/:id', () => {
    renderSection();
    const gameLink = screen.getByTestId('friends-ref-link-friend-1');
    const gameNightLink = screen.getByTestId('friends-ref-link-friend-2');
    expect(gameLink).toHaveAttribute('href', '/games/game-1');
    expect(gameNightLink).toHaveAttribute('href', '/game-nights/event-1');
  });
});
