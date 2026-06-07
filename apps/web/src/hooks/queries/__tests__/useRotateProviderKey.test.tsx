/**
 * @vitest-environment jsdom
 *
 * useRotateProviderKey hook tests (#1859 Phase 11).
 *
 * Coverage:
 *   - 200 OK → returns response body
 *   - 401 + subcode=step_up_required → kind='step_up_required'
 *   - 401 + subcode=enroll_required → kind='enroll_required'
 *   - 403 → kind='forbidden'
 *   - 409 → kind='rate_limit_exceeded'
 *   - 502 → kind='provider_probe_failed'
 *   - 400 + code=provider_name_mismatch → kind='provider_name_mismatch'
 *   - 400 + code=invalid_key_format → kind='invalid_key_format'
 *   - 400 (no code) → kind='bad_request'
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RotateProviderKeyError, useRotateProviderKey } from '../useRotateProviderKey';

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

function mockResponse(init: { status: number; body?: unknown; ok?: boolean }) {
  const ok = init.ok ?? (init.status >= 200 && init.status < 300);
  return {
    ok,
    status: init.status,
    json: () => Promise.resolve(init.body ?? {}),
  } as unknown as Response;
}

const VALID_PAYLOAD = {
  newApiKey: 'sk-1234567890',
  confirmedProviderName: 'deepseek',
};

describe('useRotateProviderKey', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns the response body on 200 OK', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 200,
        body: {
          providerName: 'deepseek',
          newKeyFingerprint: 'sk-1234ab',
          rotatedAt: '2026-06-06T10:00:00Z',
          previousKeyDisabledAt: '2026-06-06T10:00:00Z',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    const response = await result.current.mutateAsync(VALID_PAYLOAD);
    expect(response.providerName).toBe('deepseek');
    expect(response.newKeyFingerprint).toBe('sk-1234ab');
  });

  it('classifies 401 + subcode=step_up_required as step_up_required', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 401,
        body: {
          error: 'two_factor_required',
          subcode: 'step_up_required',
          message: 'Step-up required',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('step_up_required');
    expect(result.current.error?.statusCode).toBe(401);
  });

  it('classifies 401 + subcode=enroll_required as enroll_required', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 401,
        body: {
          error: 'two_factor_required',
          subcode: 'enroll_required',
          message: '2FA not enrolled',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('enroll_required');
  });

  it('classifies 403 as forbidden', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 403,
        body: { error: 'forbidden', message: 'Only superadmins may rotate keys.' },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('forbidden');
  });

  it('classifies 409 as rate_limit_exceeded', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 409,
        body: {
          error: 'rate_limit_exceeded',
          code: 'rate_limit_exceeded',
          message: 'Rotated within the last 24h',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('rate_limit_exceeded');
  });

  it('classifies 502 as provider_probe_failed', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 502,
        body: {
          error: 'provider_probe_failed',
          code: 'provider_probe_failed',
          message: 'Probe failed',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('provider_probe_failed');
  });

  it('classifies 400 + code=provider_name_mismatch correctly', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 400,
        body: {
          error: 'provider_name_mismatch',
          code: 'provider_name_mismatch',
          message: 'Confirmed name does not match',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('provider_name_mismatch');
  });

  it('classifies 400 + code=invalid_key_format correctly', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 400,
        body: {
          error: 'invalid_key_format',
          code: 'invalid_key_format',
          message: 'API key too short',
        },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('invalid_key_format');
  });

  it('classifies 400 without a code as bad_request', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      mockResponse({
        status: 400,
        body: { message: 'Bad request' },
      })
    );

    const { result } = renderHook(() => useRotateProviderKey('deepseek'), {
      wrapper: createWrapper(),
    });

    await expect(result.current.mutateAsync(VALID_PAYLOAD)).rejects.toBeInstanceOf(
      RotateProviderKeyError
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('bad_request');
  });
});
