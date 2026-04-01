/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useUserFeatures, featureFlagKeys } from '../useFeatureFlags';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    featureFlags: {
      getUserFeatures: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockGetUserFeatures = api.featureFlags.getUserFeatures as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('featureFlagKeys', () => {
  it('returns stable key for user features', () => {
    expect(featureFlagKeys.userFeatures()).toEqual(['feature-flags', 'user']);
  });
});

describe('useUserFeatures', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns feature flags on success', async () => {
    const mockFlags = { pdfUpload: true, aiChat: false };
    mockGetUserFeatures.mockResolvedValue(mockFlags);

    const { result } = renderHook(() => useUserFeatures(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockFlags);
  });

  it('enters error state when API call fails', async () => {
    mockGetUserFeatures.mockRejectedValue(new Error('Forbidden'));

    const { result } = renderHook(() => useUserFeatures(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Forbidden');
  });

  it('starts in loading state', () => {
    mockGetUserFeatures.mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useUserFeatures(), { wrapper: createWrapper() });

    expect(result.current.isPending).toBe(true);
  });
});
