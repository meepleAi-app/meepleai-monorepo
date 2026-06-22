/**
 * @vitest-environment jsdom
 *
 * useStepUpTwoFactor hook tests (#1859 Phase 10).
 *
 * Coverage:
 *   - Success (200) returns { success: true, lastTotpVerifiedAt }
 *   - 401 + subcode=invalid_code → StepUpTwoFactorError kind='invalid_code'
 *   - 401 + subcode=locked_out + retryAfterSeconds → kind='locked_out' + retryAfter preserved
 *   - 503 → kind='unavailable'
 *   - Non-JSON body falls back to generic error message
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StepUpTwoFactorError, useStepUpTwoFactor } from '../useStepUpTwoFactor';

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

function mockFetchResponse(init: {
  status: number;
  ok?: boolean;
  body?: unknown;
  bodyIsInvalidJson?: boolean;
}) {
  const ok = init.ok ?? (init.status >= 200 && init.status < 300);
  return {
    ok,
    status: init.status,
    json: () =>
      init.bodyIsInvalidJson
        ? Promise.reject(new Error('Invalid JSON'))
        : Promise.resolve(init.body ?? {}),
  } as unknown as Response;
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useStepUpTwoFactor', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns success body on 200 OK', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockFetchResponse({
        status: 200,
        body: {
          success: true,
          lastTotpVerifiedAt: '2026-06-06T10:00:00Z',
        },
      })
    );

    const { result } = renderHook(() => useStepUpTwoFactor(), {
      wrapper: createWrapper(),
    });

    const response = await result.current.mutateAsync({ code: '123456' });
    expect(response.success).toBe(true);
    expect(response.lastTotpVerifiedAt).toBe('2026-06-06T10:00:00Z');
  });

  it('throws StepUpTwoFactorError with kind="invalid_code" on 401 + subcode=invalid_code', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockFetchResponse({
        status: 401,
        body: {
          error: 'two_factor_required',
          subcode: 'invalid_code',
          message: 'Invalid or expired verification code.',
        },
      })
    );

    const { result } = renderHook(() => useStepUpTwoFactor(), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync({ code: '999999' })).rejects.toBeInstanceOf(
      StepUpTwoFactorError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    const error = result.current.error;
    expect(error).toBeInstanceOf(StepUpTwoFactorError);
    expect(error?.kind).toBe('invalid_code');
    expect(error?.statusCode).toBe(401);
  });

  it('throws kind="locked_out" with retryAfterSeconds preserved on 401 + subcode=locked_out', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockFetchResponse({
        status: 401,
        body: {
          error: 'two_factor_required',
          subcode: 'locked_out',
          message: 'Too many failed attempts.',
          retryAfterSeconds: 900,
        },
      })
    );

    const { result } = renderHook(() => useStepUpTwoFactor(), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync({ code: '111111' })).rejects.toBeInstanceOf(
      StepUpTwoFactorError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('locked_out');
    expect(result.current.error?.retryAfterSeconds).toBe(900);
  });

  it('throws kind="unavailable" on 503', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockFetchResponse({
        status: 503,
        body: {
          error: 'two_factor_unavailable',
          message: 'Service down',
        },
      })
    );

    const { result } = renderHook(() => useStepUpTwoFactor(), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync({ code: '123456' })).rejects.toBeInstanceOf(
      StepUpTwoFactorError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('unavailable');
    expect(result.current.error?.statusCode).toBe(503);
  });

  it('falls back to generic message when error body is not JSON', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockFetchResponse({
        status: 401,
        bodyIsInvalidJson: true,
      })
    );

    const { result } = renderHook(() => useStepUpTwoFactor(), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync({ code: '123456' })).rejects.toBeInstanceOf(
      StepUpTwoFactorError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toMatch(/Step-up verification failed/);
  });
});
