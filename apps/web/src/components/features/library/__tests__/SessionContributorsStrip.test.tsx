/**
 * SessionContributorsStrip — render + overflow + a11y smoke tests (#2036).
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { SessionContributorsStrip } from '../SessionContributorsStrip';

import type { SessionContributorDto } from '@/lib/api/schemas';

function makeContributor(
  partial: Partial<SessionContributorDto> & { userId: string; displayName: string },
): SessionContributorDto {
  return {
    userId: partial.userId,
    displayName: partial.displayName,
    initials: partial.initials ?? partial.displayName.slice(0, 2).toUpperCase(),
    sessionCount: partial.sessionCount ?? 1,
  };
}

describe('SessionContributorsStrip', () => {
  it('renders nothing when there are no contributors', () => {
    const { container } = render(<SessionContributorsStrip contributors={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders the player-count footer (plural)', () => {
    render(
      <SessionContributorsStrip
        contributors={[
          makeContributor({ userId: 'a1b2c3d4-1111-4111-8111-111111111111', displayName: 'Alice' }),
          makeContributor({ userId: 'a1b2c3d4-2222-4222-8222-222222222222', displayName: 'Bob' }),
        ]}
      />,
    );
    expect(screen.getByText('Top contributors')).toBeInTheDocument();
    expect(screen.getByText('2 players')).toBeInTheDocument();
  });

  it('renders singular "player" when there is exactly one contributor', () => {
    render(
      <SessionContributorsStrip
        contributors={[
          makeContributor({ userId: 'a1b2c3d4-1111-4111-8111-111111111111', displayName: 'Solo' }),
        ]}
      />,
    );
    expect(screen.getByText('1 player')).toBeInTheDocument();
  });

  it('renders up to `max` avatars and an overflow chip when contributors exceed max', () => {
    const contributors = Array.from({ length: 10 }, (_, i) =>
      makeContributor({
        userId: `a1b2c3d4-${(i + 1).toString().padStart(4, '0')}-4111-8111-111111111111`,
        displayName: `User ${i + 1}`,
        initials: `U${i + 1}`,
      }),
    );

    render(<SessionContributorsStrip contributors={contributors} max={8} />);

    expect(screen.getAllByTestId('session-contributor-avatar')).toHaveLength(8);
    const overflow = screen.getByTestId('session-contributors-overflow');
    expect(overflow).toHaveTextContent('+2');
    expect(overflow).toHaveAttribute('aria-label', '2 more contributors');
  });

  it('compact mode caps visible avatars at 5', () => {
    const contributors = Array.from({ length: 8 }, (_, i) =>
      makeContributor({
        userId: `a1b2c3d4-${(i + 1).toString().padStart(4, '0')}-4111-8111-111111111111`,
        displayName: `User ${i + 1}`,
      }),
    );

    render(<SessionContributorsStrip contributors={contributors} compact />);

    expect(screen.getAllByTestId('session-contributor-avatar')).toHaveLength(5);
    expect(screen.getByTestId('session-contributors-overflow')).toHaveTextContent('+3');
  });

  it('avatar carries an accessible name + session-count tooltip', () => {
    render(
      <SessionContributorsStrip
        contributors={[
          makeContributor({
            userId: 'a1b2c3d4-aaaa-4111-8111-111111111111',
            displayName: 'Alice Anderson',
            initials: 'AA',
            sessionCount: 3,
          }),
        ]}
      />,
    );

    const strip = screen.getByTestId('session-contributors-strip');
    const avatar = within(strip).getByTestId('session-contributor-avatar');
    expect(avatar).toHaveAttribute('aria-label', 'Alice Anderson, 3 sessions');
    expect(avatar).toHaveAttribute('title', expect.stringContaining('Alice Anderson'));
    expect(avatar).toHaveAttribute('title', expect.stringContaining('3 sessions'));
    expect(avatar).toHaveTextContent('AA');
  });

  it('uses singular "session" tooltip when the user has exactly one session', () => {
    render(
      <SessionContributorsStrip
        contributors={[
          makeContributor({
            userId: 'a1b2c3d4-bbbb-4111-8111-111111111111',
            displayName: 'Bob',
            initials: 'B',
            sessionCount: 1,
          }),
        ]}
      />,
    );
    const avatar = screen.getByTestId('session-contributor-avatar');
    expect(avatar).toHaveAttribute('title', expect.stringContaining('1 session'));
    expect(avatar.getAttribute('title')).not.toContain('1 sessions');
  });

  it('orders avatars in the same order as the input list', () => {
    const contributors = [
      makeContributor({ userId: 'a1b2c3d4-1111-4111-8111-111111111111', displayName: 'Alice', initials: 'A' }),
      makeContributor({ userId: 'a1b2c3d4-2222-4222-8222-222222222222', displayName: 'Bob', initials: 'B' }),
      makeContributor({ userId: 'a1b2c3d4-3333-4333-8333-333333333333', displayName: 'Carol', initials: 'C' }),
    ];

    render(<SessionContributorsStrip contributors={contributors} />);

    const avatars = screen.getAllByTestId('session-contributor-avatar');
    expect(avatars[0]).toHaveTextContent('A');
    expect(avatars[1]).toHaveTextContent('B');
    expect(avatars[2]).toHaveTextContent('C');
  });
});
