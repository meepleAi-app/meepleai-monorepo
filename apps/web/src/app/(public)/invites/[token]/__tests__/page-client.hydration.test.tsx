/**
 * @vitest-environment jsdom
 */

import { act, type ReactElement } from 'react';

import { renderToString } from 'react-dom/server';
import { hydrateRoot } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { InvitesTokenPageClient } from '../page-client';

// Public invitation page, rendered on the server (no knowledge of the guest's timezone) and hydrated in the
// browser. The server said 20:00 (its UTC), a guest in another zone got 21:00 on the client: React error #418 and an
// hour-off first paint. And a night with no player limit read "Posti 1 / 0".

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const invitationMock = vi.fn();
// The zone the "runtime" formats in when the caller does not pass `timeZone`: the server in UTC, a guest elsewhere.
// Not process.env.TZ: a worker thread (vitest --pool=threads) changing it does not reach V8.
const runtime = vi.hoisted(() => ({ zone: 'UTC' }));

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
  // Same contract as react-intl's formatters: honours `timeZone` in the options, otherwise the runtime's zone.
  const format = (d: Date, o: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('it-IT', { timeZone: runtime.zone, ...o }).format(d);
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

const BASE_INVITATION: PublicGameNightInvitation = {
  token: 'tok-1',
  status: 'Pending',
  expiresAt: '2026-12-31T23:59:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000601',
  hostDisplayName: 'Anna Rossi',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-4000-8000-000000000611',
  title: 'Serata Catan di Anna',
  scheduledAt: '2026-11-01T20:00:00.000Z',
  location: 'Via dei Giochi 12',
  durationMinutes: null,
  expectedPlayers: 4,
  acceptedSoFar: 1,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
};

function page(): ReactElement {
  return <InvitesTokenPageClient token="tok-1" initialData={invitationMock()} />;
}

beforeEach(() => {
  invitationMock.mockReturnValue(BASE_INVITATION);
  runtime.zone = 'UTC';
});

afterEach(() => {
  document.body.innerHTML = '';
});

/** Server render in UTC, then hydrate in `clientTz` (a guest elsewhere). */
async function serverThenHydrate(clientTz: string) {
  runtime.zone = 'UTC';
  const html = renderToString(page());

  runtime.zone = clientTz;
  const container = document.createElement('div');
  container.innerHTML = html;
  document.body.appendChild(container);

  const recoverable: unknown[] = [];
  const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
  await act(async () => {
    hydrateRoot(container, page(), { onRecoverableError: e => recoverable.push(e) });
  });
  const consoleErrors = consoleError.mock.calls.map(c => String(c[0]));
  consoleError.mockRestore();
  return { html, container, recoverable, consoleErrors };
}

describe('InvitesTokenPageClient — time on the server and in the browser', () => {
  it('hydrates a guest in another timezone without a mismatch', async () => {
    const { recoverable, consoleErrors } = await serverThenHydrate('Asia/Tokyo');
    expect(recoverable).toEqual([]);
    expect(consoleErrors.filter(m => /hydrat|did not match|418/i.test(m))).toEqual([]);
  });

  it('says on the server HTML that the time is UTC', () => {
    runtime.zone = 'UTC';
    const html = renderToString(page());
    expect(html).toContain('20:00');
    expect(html).toContain('UTC');
  });

  it("replaces it with the guest's own time once mounted", async () => {
    const { container } = await serverThenHydrate('Asia/Tokyo');
    // 2026-11-01T20:00Z is 05:00 on 2 November in Tokyo; the UTC label is gone.
    expect(container.textContent).toContain('05:00');
    expect(container.textContent).not.toContain('UTC');
  });
});

describe('InvitesTokenPageClient — seats', () => {
  it('shows "accepted / expected" when the night has a player limit', () => {
    const html = renderToString(page());
    expect(html).toContain('pages.invites.session.playersValue:');
    expect(html).not.toContain('playersValueNoLimit');
  });

  it('shows only the confirmed count when the night has no player limit (expectedPlayers is 0)', () => {
    invitationMock.mockReturnValue({ ...BASE_INVITATION, expectedPlayers: 0, acceptedSoFar: 1 });
    const html = renderToString(page());
    expect(html).toContain('pages.invites.session.playersValueNoLimit:');
    expect(html).not.toContain('pages.invites.session.playersValue:');
  });
});
