/**
 * useRespondToInvitation — TanStack Query mutation hook tests (Wave A.5b, Issue #611).
 *
 * Covers the 5-state FSM contract from `useRespondToInvitation.ts`:
 *   - `pending` mutation status → `submitting`
 *   - `error` status → `error`
 *   - `success` + `result.kind === 'success'` → `success`
 *   - `success` + `result.kind === 'conflict-state-switch'` → `conflict`
 *   - `success` + `result.kind === 'gone'` → `gone`
 *   - default (idle status) → `idle`
 *
 * Plus the `submit()` swallow-rejection contract: errors surface via `error`
 * field, never as unhandled promise rejections (page-client form must stay
 * re-enabled without crashing).
 *
 * Pattern: mock `useMutation` directly (mirrors `useGameNightInvitation.test.ts`)
 * to avoid needing a QueryClientProvider wrapper for state inspection.
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api/game-night-invitations', async () => {
  const actual = await vi.importActual<typeof import('@/lib/api/game-night-invitations')>(
    '@/lib/api/game-night-invitations'
  );
  return {
    ...actual,
    respondToInvitation: vi.fn(),
  };
});

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useMutation: vi.fn(),
  };
});

const { useMutation } = await import('@tanstack/react-query');

import {
  respondToInvitation,
  type PublicGameNightInvitation,
  type RespondToInvitationResult,
} from '@/lib/api/game-night-invitations';

import { useRespondToInvitation } from '../useRespondToInvitation';

const FIXTURE: PublicGameNightInvitation = {
  token: 'tok-123',
  status: 'Accepted',
  expiresAt: '2026-05-30T00:00:00.000Z',
  respondedAt: '2026-04-29T12:00:00.000Z',
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Alex Host',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Friday Game Night',
  scheduledAt: '2026-05-15T20:00:00.000Z',
  location: null,
  durationMinutes: null,
  expectedPlayers: 4,
  acceptedSoFar: 1,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: 'Accepted',
};

const SUCCESS_RESULT: RespondToInvitationResult = {
  kind: 'success',
  action: 'Accepted',
  invitation: FIXTURE,
};

const CONFLICT_RESULT: RespondToInvitationResult = {
  kind: 'conflict-state-switch',
  currentlyRespondedAs: 'Declined',
  attemptedAction: 'Accepted',
};

const GONE_RESULT: RespondToInvitationResult = {
  kind: 'gone',
  reason: 'expired',
};

interface MockMutationOptions {
  status?: 'idle' | 'pending' | 'success' | 'error';
  data?: RespondToInvitationResult | undefined;
  error?: Error | null;
  mutateAsync?: ReturnType<typeof vi.fn>;
  reset?: ReturnType<typeof vi.fn>;
}

function buildMockMutation(opts: MockMutationOptions = {}) {
  return {
    status: opts.status ?? 'idle',
    data: opts.data,
    error: opts.error ?? null,
    mutateAsync: opts.mutateAsync ?? vi.fn(),
    reset: opts.reset ?? vi.fn(),
  } as unknown as ReturnType<typeof useMutation>;
}

describe('useRespondToInvitation (Wave A.5b)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('FSM state derivation', () => {
    it('derives idle when mutation status is idle', () => {
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ status: 'idle' }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('idle');
      expect(result.current.result).toBeNull();
      expect(result.current.error).toBeNull();
    });

    it('derives submitting when mutation status is pending', () => {
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ status: 'pending' }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('submitting');
    });

    it('derives error when mutation status is error', () => {
      const err = new Error('Server unavailable');
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ status: 'error', error: err }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('error');
      expect(result.current.error).toBe(err);
    });

    it('derives success when status=success and result.kind=success', () => {
      vi.mocked(useMutation).mockReturnValue(
        buildMockMutation({ status: 'success', data: SUCCESS_RESULT })
      );

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('success');
      expect(result.current.result).toEqual(SUCCESS_RESULT);
    });

    it('derives conflict when status=success and result.kind=conflict-state-switch', () => {
      vi.mocked(useMutation).mockReturnValue(
        buildMockMutation({ status: 'success', data: CONFLICT_RESULT })
      );

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('conflict');
      expect(result.current.result).toEqual(CONFLICT_RESULT);
    });

    it('derives gone when status=success and result.kind=gone', () => {
      vi.mocked(useMutation).mockReturnValue(
        buildMockMutation({ status: 'success', data: GONE_RESULT })
      );

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('gone');
      expect(result.current.result).toEqual(GONE_RESULT);
    });

    it('falls back to idle when status=success but data is missing', () => {
      // Defensive: TanStack Query should never expose this combination, but the
      // FSM should not crash if it ever did.
      vi.mocked(useMutation).mockReturnValue(
        buildMockMutation({ status: 'success', data: undefined })
      );

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      expect(result.current.state).toBe('idle');
    });
  });

  describe('mutationFn wiring', () => {
    it('configures mutationFn to invoke respondToInvitation with token + action', async () => {
      let capturedMutationFn:
        | ((vars: {
            token: string;
            action: 'Accepted' | 'Declined';
          }) => Promise<RespondToInvitationResult>)
        | undefined;

      vi.mocked(useMutation).mockImplementation((opts: Record<string, unknown>) => {
        capturedMutationFn = opts.mutationFn as typeof capturedMutationFn;
        return buildMockMutation();
      });
      vi.mocked(respondToInvitation).mockResolvedValue(SUCCESS_RESULT);

      renderHook(() => useRespondToInvitation({ token: 'tok-xyz' }));

      expect(capturedMutationFn).toBeDefined();
      const out = await capturedMutationFn!({ token: 'tok-xyz', action: 'Accepted' });
      expect(respondToInvitation).toHaveBeenCalledWith('tok-xyz', 'Accepted');
      expect(out).toEqual(SUCCESS_RESULT);
    });
  });

  describe('submit() contract', () => {
    it('returns the RespondToInvitationResult on success', async () => {
      const mutateAsync = vi.fn().mockResolvedValue(SUCCESS_RESULT);
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ mutateAsync }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      const out = await result.current.submit('Accepted');

      expect(mutateAsync).toHaveBeenCalledWith({ token: 'tok-abc', action: 'Accepted' });
      expect(out).toEqual(SUCCESS_RESULT);
    });

    it('returns null and SWALLOWS rejection on error (no unhandled rejection)', async () => {
      const boom = new Error('5xx blew up');
      const mutateAsync = vi.fn().mockRejectedValue(boom);
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ mutateAsync }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      // Critical: must NOT throw — page-client form depends on this so a 5xx
      // doesn't crash with an unhandled promise rejection.
      const out = await result.current.submit('Declined');

      expect(out).toBeNull();
      expect(mutateAsync).toHaveBeenCalledWith({ token: 'tok-abc', action: 'Declined' });
    });

    it('passes the supplied action through to mutateAsync', async () => {
      const mutateAsync = vi.fn().mockResolvedValue(SUCCESS_RESULT);
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ mutateAsync }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      await result.current.submit('Accepted');
      await result.current.submit('Declined');

      expect(mutateAsync).toHaveBeenNthCalledWith(1, { token: 'tok-abc', action: 'Accepted' });
      expect(mutateAsync).toHaveBeenNthCalledWith(2, { token: 'tok-abc', action: 'Declined' });
    });
  });

  describe('reset() contract', () => {
    it('invokes the underlying mutation.reset', () => {
      const reset = vi.fn();
      vi.mocked(useMutation).mockReturnValue(buildMockMutation({ reset }));

      const { result } = renderHook(() => useRespondToInvitation({ token: 'tok-abc' }));

      result.current.reset();

      expect(reset).toHaveBeenCalledOnce();
    });
  });
});
