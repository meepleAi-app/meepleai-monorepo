/**
 * useCanViewPdf — unit tests
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.4
 */
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getGameDocuments: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { useCanViewPdf } from '../useCanViewPdf';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const sampleDocument = {
  id: 'pdf-wingspan-123',
  title: 'Wingspan Manuale base',
  status: 'indexed' as const,
  pageCount: 24,
  createdAt: '2026-03-08T10:00:00Z',
  category: 'Rulebook' as const,
  versionLabel: 'v1.2',
};

describe('useCanViewPdf', () => {
  beforeEach(() => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReset();
  });

  it('returns canView=false without fetching when enabled=false', () => {
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1', gameId: 'game-1', enabled: false }),
      { wrapper }
    );
    expect(result.current.canView).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('returns canView=false without fetching when gameId missing', () => {
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1' }),
      { wrapper }
    );
    expect(result.current.canView).toBe(false);
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('returns canView=true when documentId is in user documents', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-wingspan-123', gameId: 'game-wingspan' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.canView).toBe(true);
  });

  it('returns canView=false when documentId is NOT in user documents', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-OTHER-456', gameId: 'game-wingspan' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.canView).toBe(false);
  });

  it('returns isError=true when API rejects', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockRejectedValueOnce(new Error('500'));
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1', gameId: 'game-1' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.canView).toBe(false);
  });
});
