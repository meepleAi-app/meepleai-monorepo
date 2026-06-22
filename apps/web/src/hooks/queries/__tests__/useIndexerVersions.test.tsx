import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import { useIndexerVersions } from '../useIndexerVersions';

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getIndexerVersions: vi.fn(),
    },
  },
}));

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('useIndexerVersions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the registry list on success', async () => {
    vi.mocked(api.pdf.getIndexerVersions).mockResolvedValue([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);

    const { result } = renderHook(() => useIndexerVersions(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);
  });

  it('exposes errors via isError', async () => {
    vi.mocked(api.pdf.getIndexerVersions).mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useIndexerVersions(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('boom');
  });
});
