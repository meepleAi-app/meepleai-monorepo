/**
 * @vitest-environment jsdom
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import { PublicFooter } from '@/components/layouts/PublicFooter';
import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { InvitesTokenPageClient } from '../page-client';

// #4916. The invitation page has an h1 and no h2: the page, then the footer the public layout puts after it. With the
// footer's titles at h3, axe reported «Heading order invalid» on this page (and on /contact and /pricing, which have the
// same shape). The test renders the page with the footer, the way the layout does, and applies the rule to both.

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
  useGameNightInvitation: ({ initialData }: { initialData?: unknown }) => ({
    data: initialData,
    refetch: vi.fn(),
  }),
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

const INVITATION: PublicGameNightInvitation = {
  token: 'tok-1',
  status: 'Pending',
  expiresAt: '2026-12-31T23:59:00.000Z',
  respondedAt: null,
  hostUserId: '6767c94f-6519-4580-99f2-75ac73c7e910',
  hostDisplayName: 'Anna Rossi',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '11111111-1111-4111-8111-111111111111',
  title: 'Serata del sabato',
  scheduledAt: '2026-12-12T19:30:00+00:00',
  location: 'Casa di Anna',
  durationMinutes: null,
  expectedPlayers: 4,
  acceptedSoFar: 1,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
  respondedByName: null,
};

describe('InvitesTokenPageClient — heading order (#4916)', () => {
  it('passes the axe heading-order rule with the public footer after it', async () => {
    const { container } = render(
      <>
        <main>
          <InvitesTokenPageClient token="tok-1" initialData={INVITATION} />
        </main>
        <PublicFooter />
      </>
    );

    const results = await axe(container, { runOnly: { type: 'rule', values: ['heading-order'] } });

    expect(results).toHaveNoViolations();
  });
});
