/**
 * @vitest-environment jsdom
 *
 * useInstallToolkit unit tests — Wave 3 Phase 2 (Issue #805 / PR #732 §5.3.5).
 *
 * Coverage:
 *   - POST /toolkits/{id}/install with empty body.
 *   - Schema validation forwards typed payload.
 *   - Idempotency contract: repeated mutateAsync calls succeed.
 *   - Detail-cache invalidation runs on success (so viewerContext.has
 *     Installed flips on next render).
 *   - Error path surfaces the rejection.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { toolkitDetailKeys } from '../useToolkitDetail';
import { useInstallToolkit } from '../useInstallToolkit';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

const mockApi = vi.hoisted<MockedApiClient>(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  head: vi.fn(),
  options: vi.fn(),
}));
vi.mock('@/lib/api/client', () => ({ apiClient: mockApi }));

const mockPost = mockApi.post;

const TOOLKIT_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_INSTALL = {
  installCount: 0,
  hasInstalled: true,
};

function renderInstallHook() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const { result } = renderHook(() => useInstallToolkit(), { wrapper: Wrapper });
  return { result, queryClient };
}

describe('useInstallToolkit — request shape', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPost.mockResolvedValue(SAMPLE_INSTALL);
  });

  it('POSTs to /toolkits/{id}/install with no body', async () => {
    const { result } = renderInstallHook();
    await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });

    expect(mockPost).toHaveBeenCalledWith(
      `/api/v1/toolkits/${TOOLKIT_ID}/install`,
      undefined,
      expect.anything()
    );
  });

  it('returns the parsed install response on success', async () => {
    const { result } = renderInstallHook();
    const response = await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });

    expect(response).toEqual(SAMPLE_INSTALL);
  });
});

describe('useInstallToolkit — idempotency', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPost.mockResolvedValue(SAMPLE_INSTALL);
  });

  it('repeated installs all return success (PR #732 §5.3.5 Nygard)', async () => {
    const { result } = renderInstallHook();
    const r1 = await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });
    const r2 = await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });
    const r3 = await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });

    expect(r1.hasInstalled).toBe(true);
    expect(r2.hasInstalled).toBe(true);
    expect(r3.hasInstalled).toBe(true);
    expect(mockPost).toHaveBeenCalledTimes(3);
  });
});

describe('useInstallToolkit — cache invalidation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPost.mockResolvedValue(SAMPLE_INSTALL);
  });

  it('invalidates the toolkit-detail query on success', async () => {
    const { result, queryClient } = renderInstallHook();
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    await result.current.mutateAsync({ toolkitId: TOOLKIT_ID });

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: toolkitDetailKeys.byId(TOOLKIT_ID),
      });
    });
  });
});

describe('useInstallToolkit — error handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('surfaces an error on rejection', async () => {
    mockPost.mockRejectedValue(new Error('install failed'));
    const { result } = renderInstallHook();

    await expect(result.current.mutateAsync({ toolkitId: TOOLKIT_ID })).rejects.toThrow(
      'install failed'
    );
  });
});
