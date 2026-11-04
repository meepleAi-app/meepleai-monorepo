/**
 * @vitest-environment jsdom
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { InvitesTokenPageClient } from '../page-client';

// #4856. A 410 from the respond endpoint carries no reason, so after it the page reads the invitation again: the
// fresh `nightCancelled` is how it tells a cancelled night (shown as such) from an expired invitation.

const refetch = vi.fn();
const submit = vi.fn();

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
  useGameNightInvitation: () => ({ data: INVITATION, refetch }),
}));
vi.mock('@/hooks/useRespondToInvitation', () => ({
  useRespondToInvitation: () => ({ state: 'idle', result: null, submit }),
}));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, vars?: Record<string, unknown>) =>
      vars ? `${key}:${JSON.stringify(vars)}` : key,
    formatDate: (d: Date) => d.toISOString(),
    formatTime: (d: Date) => d.toISOString(),
    formatMessage: ({ id }: { id: string }) => id,
    locale: 'it-IT',
  }),
}));

const INVITATION: PublicGameNightInvitation = {
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
  scheduledAt: '2026-12-01T20:00:00.000Z',
  location: 'Via dei Giochi 12',
  durationMinutes: null,
  expectedPlayers: 4,
  acceptedSoFar: 1,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
};

beforeEach(() => {
  refetch.mockReset().mockResolvedValue(undefined);
  submit.mockReset();
});

describe('InvitesTokenPageClient — after a 410', () => {
  it.each([
    ['accepts', 'pages.invites.form.acceptCta'],
    ['declines', 'pages.invites.form.declineCta'],
  ])('reads the invitation again when the guest %s and the answer is 410', async (_name, label) => {
    submit.mockResolvedValue({ kind: 'gone', reason: 'expired' });
    render(<InvitesTokenPageClient token="tok-1" initialData={INVITATION} />);

    fireEvent.click(screen.getByText(label));

    await waitFor(() => expect(refetch).toHaveBeenCalledTimes(1));
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('does not read it again after a plain success', async () => {
    submit.mockResolvedValue({ kind: 'success', action: 'Accepted', invitation: INVITATION });
    render(<InvitesTokenPageClient token="tok-1" initialData={INVITATION} />);

    fireEvent.click(screen.getByText('pages.invites.form.acceptCta'));

    await waitFor(() => expect(submit).toHaveBeenCalledTimes(1));
    expect(refetch).not.toHaveBeenCalled();
  });
});
