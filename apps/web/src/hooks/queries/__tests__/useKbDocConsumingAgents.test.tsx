import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api/admin-kb-used-by', () => ({
  fetchKbDocConsumingAgents: vi.fn(),
}));

import { fetchKbDocConsumingAgents } from '@/lib/api/admin-kb-used-by';
import { useKbDocConsumingAgents } from '../useKbDocConsumingAgents';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('useKbDocConsumingAgents', () => {
  beforeEach(() => {
    vi.mocked(fetchKbDocConsumingAgents).mockReset();
  });

  it('does not fetch when docId is null', () => {
    const { result } = renderHook(() => useKbDocConsumingAgents({ docId: null }), {
      wrapper: wrapper(),
    });
    expect(result.current.fetchStatus).toBe('idle');
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('does not fetch when docId is empty string', () => {
    renderHook(() => useKbDocConsumingAgents({ docId: '' }), { wrapper: wrapper() });
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('does not fetch when enabled=false', () => {
    renderHook(() => useKbDocConsumingAgents({ docId: 'doc-1', enabled: false }), {
      wrapper: wrapper(),
    });
    expect(fetchKbDocConsumingAgents).not.toHaveBeenCalled();
  });

  it('fetches and returns the agents when docId is valid and enabled', async () => {
    vi.mocked(fetchKbDocConsumingAgents).mockResolvedValue([
      {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Alpha',
        type: 'HybridSearch',
        isActive: true,
        status: 'Published',
        isSystemDefined: false,
        typologySlug: null,
        gameId: null,
        gameName: null,
        invocationCount: 0,
        lastInvokedAt: null,
      },
    ]);

    const { result } = renderHook(() => useKbDocConsumingAgents({ docId: 'doc-1' }), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].name).toBe('Alpha');
    expect(fetchKbDocConsumingAgents).toHaveBeenCalledWith('doc-1');
  });
});
