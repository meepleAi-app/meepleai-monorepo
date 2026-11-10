/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { InvitesTokenPageClient } from '../page-client';

// #4910. Two things the live invitation page got wrong and nobody saw, because the nights the tests used all had a game:
//  - for a night with no game the heading ended at «ti invita a giocare a» and the accepted summary said «—»;
//  - the page opened its own <main> inside the one the public layout already provides (axe: «Document has more than one
//    main landmark»).

let stateParam: string | null = null;

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({ get: (key: string) => (key === 'state' ? stateParam : null) }),
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

/** A live invitation to a night that has a title and no game. */
const NIGHT_WITHOUT_GAME: PublicGameNightInvitation = {
  token: 'tok-1',
  status: 'Pending',
  expiresAt: '2026-12-31T23:59:00.000Z',
  respondedAt: null,
  hostUserId: '6767c94f-6519-4580-99f2-75ac73c7e910',
  hostDisplayName: 'Anna Rossi',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '11111111-1111-1111-1111-111111111111',
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

const NIGHT_WITH_GAME: PublicGameNightInvitation = {
  ...NIGHT_WITHOUT_GAME,
  primaryGameId: '22222222-2222-2222-2222-222222222222',
  primaryGameName: 'Catan',
};

function renderPage(invitation: PublicGameNightInvitation) {
  return render(<InvitesTokenPageClient token="tok-1" initialData={invitation} />);
}

function headingText(): string {
  return (screen.getByRole('heading', { level: 1 }).textContent ?? '').replace(/\s+/g, ' ').trim();
}

beforeEach(() => {
  stateParam = null;
});

describe('InvitesTokenPageClient — what the invitation is to', () => {
  it('says «giocare a <game>» when the night has a game', () => {
    renderPage(NIGHT_WITH_GAME);

    expect(headingText()).toBe('Anna Rossi ti invita a giocare a Catan');
  });

  it('names the night itself, in a sentence that fits it, when it has no game', () => {
    renderPage(NIGHT_WITHOUT_GAME);

    expect(headingText()).toBe('Anna Rossi ti invita a Serata del sabato');
  });

  it('says the same on the page of a guest who has already answered', () => {
    renderPage({ ...NIGHT_WITHOUT_GAME, alreadyRespondedAs: 'Accepted' });

    expect(headingText()).toBe('Anna Rossi ti invita a Serata del sabato');
  });

  it('puts the title of the night in the accepted summary instead of a dash when there is no game', () => {
    stateParam = 'accepted-success';

    const { container } = renderPage(NIGHT_WITHOUT_GAME);

    const summary = container.querySelector('[data-slot="invite-accepted-success-shell"]');
    expect(summary?.textContent).toContain('Serata del sabato');
    expect(summary?.textContent).not.toContain('—');
  });
});

describe('InvitesTokenPageClient — landmarks', () => {
  it.each([null, 'accepted-success', 'declined', 'token-expired', 'token-invalid'])(
    'does not open a <main> of its own (state %j): the public layout owns it',
    state => {
      stateParam = state;

      const { container } = renderPage(NIGHT_WITHOUT_GAME);

      expect(container.querySelector('main')).toBeNull();
    }
  );

  it('is still a named region, so a screen reader can find it', () => {
    renderPage(NIGHT_WITHOUT_GAME);

    const region = screen.getByRole('region', { name: 'pages.invites.states.default.ariaRegion' });
    expect(region).toHaveAttribute('data-slot', 'invites-token-page');
  });
});
