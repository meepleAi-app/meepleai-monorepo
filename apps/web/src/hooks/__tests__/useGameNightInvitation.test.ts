/**
 * useGameNightInvitation — TanStack Query hook tests (Wave A.5b, Issue #611).
 *
 * Verifies the SSR-seeded query contract from spec §3.4:
 *   - Stable `queryKey` keyed on token
 *   - `initialData` propagation (SSR seed)
 *   - `staleTime: 60_000` aligned with HybridCache TTL
 *   - `enabled: token.length > 0` to defend router transitions
 *   - `retry: false` for InvitationNotFoundError (404 is structural UX state)
 *
 * Pattern: mock `useQuery` directly (mirrors `useKbGameDocuments.test.ts`)
 * to avoid needing a QueryClientProvider wrapper for config inspection.
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api/game-night-invitations', async () => {
  const actual = await vi.importActual<typeof import('@/lib/api/game-night-invitations')>(
    '@/lib/api/game-night-invitations'
  );
  return {
    ...actual,
    getInvitation: vi.fn(),
  };
});

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useQuery: vi.fn(),
  };
});

const { useQuery } = await import('@tanstack/react-query');

import {
  getInvitation,
  InvitationFetchError,
  InvitationNotFoundError,
  type PublicGameNightInvitation,
} from '@/lib/api/game-night-invitations';

import { useGameNightInvitation } from '../useGameNightInvitation';

const FIXTURE: PublicGameNightInvitation = {
  token: 'tok-123',
  status: 'Pending',
  expiresAt: '2026-05-30T00:00:00.000Z',
  respondedAt: null,
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
  acceptedSoFar: 0,
  primaryGameId: null,
  primaryGameName: null,
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
};

describe('useGameNightInvitation (Wave A.5b)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('configures stable query key keyed on token', () => {
    vi.mocked(useQuery).mockReturnValue({
      data: undefined,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn().mockResolvedValue({ data: undefined }),
    } as unknown as ReturnType<typeof useQuery>);

    renderHook(() => useGameNightInvitation({ token: 'tok-abc' }));

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: ['game-night-invitation', 'tok-abc'],
        staleTime: 60_000,
        enabled: true,
      })
    );
  });

  it('propagates initialData (SSR seed) to React Query', () => {
    vi.mocked(useQuery).mockReturnValue({
      data: FIXTURE,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn().mockResolvedValue({ data: FIXTURE }),
    } as unknown as ReturnType<typeof useQuery>);

    const { result } = renderHook(() =>
      useGameNightInvitation({ token: 'tok-abc', initialData: FIXTURE })
    );

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        initialData: FIXTURE,
      })
    );
    expect(result.current.data).toEqual(FIXTURE);
  });

  it('disables query when token is empty (router transition guard)', () => {
    vi.mocked(useQuery).mockReturnValue({
      data: undefined,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn().mockResolvedValue({ data: undefined }),
    } as unknown as ReturnType<typeof useQuery>);

    renderHook(() => useGameNightInvitation({ token: '' }));

    expect(useQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        enabled: false,
      })
    );
  });

  it('queryFn invokes getInvitation with the supplied token', async () => {
    let capturedQueryFn: (() => Promise<PublicGameNightInvitation>) | undefined;
    vi.mocked(useQuery).mockImplementation((opts: Record<string, unknown>) => {
      capturedQueryFn = opts.queryFn as () => Promise<PublicGameNightInvitation>;
      return {
        data: undefined,
        isLoading: true,
        isFetching: true,
        isError: false,
        error: null,
        refetch: vi.fn(),
      } as unknown as ReturnType<typeof useQuery>;
    });
    vi.mocked(getInvitation).mockResolvedValue(FIXTURE);

    renderHook(() => useGameNightInvitation({ token: 'tok-xyz' }));

    expect(capturedQueryFn).toBeDefined();
    await capturedQueryFn!();
    expect(getInvitation).toHaveBeenCalledWith('tok-xyz');
  });

  it('does NOT retry on InvitationNotFoundError (404 is a structural UX state)', () => {
    let capturedRetry: ((failureCount: number, error: Error) => boolean) | undefined;
    vi.mocked(useQuery).mockImplementation((opts: Record<string, unknown>) => {
      capturedRetry = opts.retry as (failureCount: number, error: Error) => boolean;
      return {
        data: undefined,
        isLoading: false,
        isFetching: false,
        isError: false,
        error: null,
        refetch: vi.fn(),
      } as unknown as ReturnType<typeof useQuery>;
    });

    renderHook(() => useGameNightInvitation({ token: 'tok-abc' }));

    expect(capturedRetry).toBeDefined();
    expect(capturedRetry!(0, new InvitationNotFoundError('tok-abc'))).toBe(false);
    expect(capturedRetry!(1, new InvitationNotFoundError('tok-abc'))).toBe(false);
  });

  it('retries once on transient errors (5xx / network)', () => {
    let capturedRetry: ((failureCount: number, error: Error) => boolean) | undefined;
    vi.mocked(useQuery).mockImplementation((opts: Record<string, unknown>) => {
      capturedRetry = opts.retry as (failureCount: number, error: Error) => boolean;
      return {
        data: undefined,
        isLoading: false,
        isFetching: false,
        isError: false,
        error: null,
        refetch: vi.fn(),
      } as unknown as ReturnType<typeof useQuery>;
    });

    renderHook(() => useGameNightInvitation({ token: 'tok-abc' }));

    expect(capturedRetry).toBeDefined();
    const transient = new InvitationFetchError(503, 'Server unavailable');
    expect(capturedRetry!(0, transient)).toBe(true); // first failure → retry
    expect(capturedRetry!(1, transient)).toBe(false); // second failure → stop
  });

  it('exposes refetch as a void-returning async function', async () => {
    const innerRefetch = vi.fn().mockResolvedValue({ data: FIXTURE });
    vi.mocked(useQuery).mockReturnValue({
      data: FIXTURE,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: innerRefetch,
    } as unknown as ReturnType<typeof useQuery>);

    const { result } = renderHook(() => useGameNightInvitation({ token: 'tok-abc' }));

    await result.current.refetch();
    expect(innerRefetch).toHaveBeenCalledOnce();
  });
});
