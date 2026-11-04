/**
 * @vitest-environment jsdom
 */

import { type ReactElement } from 'react';

import { renderToString } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { InvitesTokenPageClient } from '../page-client';

// #4745, #4856. A link that is no longer alive comes back from the backend with only its state and the host's name:
// the title is blank, the time is the default date (year 1), the ids are the empty guid. The page must say «not valid
// any more» from that without breaking on the empty fields, say that the NIGHT was cancelled when it was, and not
// greet a guest who had accepted with «you already answered» for a night that is off.

const invitationMock = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({ get: () => null }),
}));
vi.mock('next/image', () => ({
  // eslint-disable-next-line @next/next/no-img-element
  default: (props: { alt: string; src: string }) => <img alt={props.alt} src={props.src} />,
}));
vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({ user: null }),
}));
vi.mock('@/hooks/useGameNightInvitation', () => ({
  useGameNightInvitation: () => ({ data: invitationMock(), refetch: vi.fn() }),
}));
vi.mock('@/hooks/useRespondToInvitation', () => ({
  useRespondToInvitation: () => ({ state: 'idle', result: null, submit: vi.fn() }),
}));
vi.mock('@/hooks/useTranslation', () => {
  const format = (d: Date, o: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('it-IT', { timeZone: 'UTC', ...o }).format(d);
  return {
    useTranslation: () => ({
      t: (key: string, vars?: Record<string, unknown>) =>
        vars ? `${key}:${JSON.stringify(vars)}` : key,
      formatDate: format,
      formatTime: format,
      formatMessage: ({ id }: { id: string }) => id,
      locale: 'it-IT',
    }),
  };
});

/** What the backend sends for a link that is no longer alive. */
const DEAD_LINK: PublicGameNightInvitation = {
  token: 'tok-1',
  status: 'Cancelled',
  expiresAt: '2026-12-31T23:59:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-0000-0000-000000000000',
  hostDisplayName: 'Anna Rossi',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-0000-0000-000000000000',
  title: '',
  scheduledAt: '0001-01-01T00:00:00+00:00',
  location: null,
  durationMinutes: null,
  expectedPlayers: 0,
  acceptedSoFar: 0,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
  respondedByName: null,
};

function page(): ReactElement {
  return <InvitesTokenPageClient token="tok-1" initialData={invitationMock()} />;
}

beforeEach(() => {
  invitationMock.mockReset();
});

describe('InvitesTokenPageClient — a link that is no longer alive', () => {
  it.each(['Cancelled', 'Expired'])(
    'a %s invitation says it is not valid any more, naming the host, and does not break on the emptied fields',
    status => {
      invitationMock.mockReturnValue({ ...DEAD_LINK, status });

      const html = renderToString(page());

      expect(html).toContain('data-slot="invite-expired"');
      expect(html).toContain('pages.invites.states.tokenExpired.heading');
      expect(html).toContain('Anna Rossi');
      expect(html).not.toContain('pages.invites.states.nightCancelled');
    }
  );

  it('a cancelled night says so, naming the host', () => {
    invitationMock.mockReturnValue({ ...DEAD_LINK, status: 'Pending', nightCancelled: true });

    const html = renderToString(page());

    expect(html).toContain('data-slot="invite-expired"');
    expect(html).toContain('pages.invites.states.nightCancelled.heading');
    expect(html).toContain('pages.invites.states.nightCancelled.subText');
    expect(html).toContain('Anna Rossi');
    expect(html).not.toContain('pages.invites.states.tokenExpired.heading');
  });

  it('a guest who had accepted is told the night is cancelled, not that they already answered', () => {
    invitationMock.mockReturnValue({
      ...DEAD_LINK,
      status: 'Accepted',
      alreadyRespondedAs: 'Accepted',
      nightCancelled: true,
    });

    const html = renderToString(page());

    expect(html).toContain('pages.invites.states.nightCancelled.heading');
    expect(html).not.toContain('pages.invites.states.alreadyAccepted');
  });

  it('with no host name it still says the night is cancelled', () => {
    invitationMock.mockReturnValue({
      ...DEAD_LINK,
      hostDisplayName: '',
      status: 'Pending',
      nightCancelled: true,
    });

    const html = renderToString(page());

    expect(html).toContain('pages.invites.states.nightCancelled.subTextNoHost');
  });

  it('shows nothing of the night: no title, no place, no time', () => {
    invitationMock.mockReturnValue({ ...DEAD_LINK, status: 'Accepted', nightCancelled: true });

    const html = renderToString(page());

    // The meta grid of the live page (date, time, place, players) is not rendered at all.
    expect(html).not.toContain('pages.invites.session');
  });
});
