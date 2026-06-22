/**
 * Tests for PublicJoinEventView (issue #1169).
 *
 * The orchestrator state machine routes between 7 surfaces:
 *   loading | token-invalid | token-expired | token-cancelled |
 *   rate-limited | generic-error | rsvp
 *
 * We mock both hooks (`useGameNightInvitation` + `useRespondToInvitation`) to
 * exercise the FSM in isolation without spinning up TanStack Query.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { IntlProvider } from 'react-intl';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import {
  InvitationGoneError,
  InvitationNotFoundError,
  InvitationRateLimitedError,
  type PublicGameNightInvitation,
} from '@/lib/api/game-night-invitations';

import { PublicJoinEventView } from '../PublicJoinEventView';

vi.mock('@/hooks/useGameNightInvitation', () => ({
  useGameNightInvitation: vi.fn(),
}));
vi.mock('@/hooks/useRespondToInvitation', () => ({
  useRespondToInvitation: vi.fn(),
}));

const { useGameNightInvitation } = await import('@/hooks/useGameNightInvitation');
const { useRespondToInvitation } = await import('@/hooks/useRespondToInvitation');

// Local provider stack — the global renderWithProviders pulls in
// ChatStoreProvider via a stale path, and this orchestrator only needs
// QueryClient + IntlProvider. Flatten the locale JSON once for react-intl.
// Direct ESM import works in Vite/Vitest for JSON modules (path alias
// `@/` doesn't resolve under CommonJS `require` in Vite-transformed tests).
// eslint-disable-next-line import/no-relative-parent-imports
import enRaw from '../../../../../../../locales/en.json';

function flattenMessages(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(obj)) {
    const full = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object') {
      Object.assign(out, flattenMessages(value as Record<string, unknown>, full));
    } else {
      out[full] = String(value);
    }
  }
  return out;
}

const enMessages = flattenMessages(enRaw as Record<string, unknown>);

function renderWithIntl(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <IntlProvider locale="en" messages={enMessages}>
        {children}
      </IntlProvider>
    </QueryClientProvider>
  );
  return render(ui, { wrapper: Wrapper });
}

const FIXTURE: PublicGameNightInvitation = {
  token: 'tok-1169',
  status: 'Pending',
  expiresAt: '2026-06-01T00:00:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Alex Host',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Saturday Game Night',
  scheduledAt: '2026-05-23T20:00:00.000Z',
  location: "Marco's place · Padova",
  durationMinutes: 180,
  expectedPlayers: 6,
  acceptedSoFar: 2,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
  respondedByName: null,
};

function mockInvitationHook(
  overrides: Partial<ReturnType<typeof useGameNightInvitation>> = {}
): ReturnType<typeof useGameNightInvitation> {
  return {
    data: FIXTURE,
    isLoading: false,
    isFetching: false,
    isError: false,
    error: null,
    refetch: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

function mockRespondHook(
  overrides: Partial<ReturnType<typeof useRespondToInvitation>> = {}
): ReturnType<typeof useRespondToInvitation> {
  return {
    state: 'idle',
    result: null,
    error: null,
    submit: vi.fn().mockResolvedValue(null),
    reset: vi.fn(),
    ...overrides,
  };
}

describe('PublicJoinEventView (issue #1169)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('surface routing', () => {
    it('renders the loading surface when the invitation query is in flight', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({ data: undefined, isLoading: true })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'loading');
    });

    it('renders InvalidTokenError surface on 404 (InvitationNotFoundError)', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: undefined,
          isError: true,
          error: new InvitationNotFoundError('tok-1169'),
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-invalid');
      expect(screen.getByText(/Invitation not found/i)).toBeInTheDocument();
    });

    it('renders ExpiredOrCancelledError(expired) on 410 from GET', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: undefined,
          isError: true,
          error: new InvitationGoneError('tok-1169'),
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-expired');
      expect(screen.getByText(/Invitation expired/i)).toBeInTheDocument();
    });

    it('renders RateLimitedError surface on 429 from GET', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: undefined,
          isError: true,
          error: new InvitationRateLimitedError(45),
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'rate-limited');
      expect(screen.getByText(/Too many requests/i)).toBeInTheDocument();
    });

    it('renders GenericError surface on 5xx / network errors', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: undefined,
          isError: true,
          error: new Error('Server unavailable'),
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'generic-error');
      expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    });

    it('renders RsvpSurface (rsvp) when invitation data is present and pending', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(mockInvitationHook());
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'rsvp');
      // Hero rendered with title from invitation.
      expect(screen.getByRole('heading', { name: FIXTURE.title })).toBeInTheDocument();
      // Form rendered with Accept / Decline CTAs.
      expect(screen.getByRole('button', { name: /I'm in/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Can't make it/i })).toBeInTheDocument();
    });

    it('routes to token-cancelled when invitation.status === "Cancelled"', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({ data: { ...FIXTURE, status: 'Cancelled' } })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-cancelled');
      expect(screen.getByText(/Game night cancelled/i)).toBeInTheDocument();
    });

    it('routes to token-expired when invitation.status === "Expired"', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({ data: { ...FIXTURE, status: 'Expired' } })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-expired');
    });
  });

  describe('mutation-driven terminal states', () => {
    it('overrides query with rate-limited when mutation returns rate-limited (issue #1169)', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(mockInvitationHook());
      vi.mocked(useRespondToInvitation).mockReturnValue(
        mockRespondHook({
          state: 'rate-limited',
          result: { kind: 'rate-limited', retryAfter: 10 },
        })
      );

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'rate-limited');
    });

    it('overrides query with token-cancelled when mutation returns gone(cancelled)', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(mockInvitationHook());
      vi.mocked(useRespondToInvitation).mockReturnValue(
        mockRespondHook({
          state: 'gone',
          result: { kind: 'gone', reason: 'cancelled' },
        })
      );

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-cancelled');
    });

    it('overrides query with token-expired when mutation returns gone(expired)', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(mockInvitationHook());
      vi.mocked(useRespondToInvitation).mockReturnValue(
        mockRespondHook({
          state: 'gone',
          result: { kind: 'gone', reason: 'expired' },
        })
      );

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      const main = screen.getByRole('main');
      expect(main).toHaveAttribute('data-surface', 'token-expired');
    });
  });

  describe('already-responded surface', () => {
    it('renders the "already responded" panel with the persisted name when alreadyRespondedAs + respondedByName', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: {
            ...FIXTURE,
            alreadyRespondedAs: 'Accepted',
            respondedByName: 'Marco',
          },
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      expect(screen.getByText(/You've already responded/i)).toBeInTheDocument();
      // The persisted name appears in the alreadyRespondedBody copy.
      expect(screen.getByText('You accepted as "Marco".')).toBeInTheDocument();
      // Create-account CTA appears after responding.
      expect(screen.getByText(/Want to keep track of your game nights/i)).toBeInTheDocument();
    });

    it('shows the anonymous-response copy when respondedByName is null', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(
        mockInvitationHook({
          data: {
            ...FIXTURE,
            alreadyRespondedAs: 'Declined',
            respondedByName: null,
          },
        })
      );
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      expect(screen.getByText('You declined this invitation.')).toBeInTheDocument();
    });
  });

  describe('public banner', () => {
    it('always renders the public banner above the content', () => {
      vi.mocked(useGameNightInvitation).mockReturnValue(mockInvitationHook());
      vi.mocked(useRespondToInvitation).mockReturnValue(mockRespondHook());

      renderWithIntl(<PublicJoinEventView code="tok-1169" />);

      expect(screen.getByText(/You're viewing a public invitation/i)).toBeInTheDocument();
    });
  });
});
