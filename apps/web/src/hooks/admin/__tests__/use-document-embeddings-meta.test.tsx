import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { PropsWithChildren } from 'react';

vi.mock('@/lib/api/admin-kb-embeddings', () => ({
  getDocumentEmbeddingsMeta: vi.fn(),
}));

import { getDocumentEmbeddingsMeta } from '@/lib/api/admin-kb-embeddings';

import { documentEmbeddingsKeys, useDocumentEmbeddingsMeta } from '../use-document-embeddings-meta';

function wrapper({ children }: PropsWithChildren) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useDocumentEmbeddingsMeta', () => {
  it('documentEmbeddingsKeys.meta returns stable query key', () => {
    expect(documentEmbeddingsKeys.meta('abc-123')).toEqual([
      'admin',
      'kb',
      'docs',
      'abc-123',
      'embeddings',
      'meta',
    ]);
  });

  it('does NOT fire fetch when enabled=false', async () => {
    vi.mocked(getDocumentEmbeddingsMeta).mockResolvedValue(null);
    renderHook(() => useDocumentEmbeddingsMeta('abc', false), { wrapper });
    await new Promise(r => setTimeout(r, 30));
    expect(getDocumentEmbeddingsMeta).not.toHaveBeenCalled();
  });

  it('does NOT fire fetch when docId is null', async () => {
    vi.mocked(getDocumentEmbeddingsMeta).mockResolvedValue(null);
    renderHook(() => useDocumentEmbeddingsMeta(null, true), { wrapper });
    await new Promise(r => setTimeout(r, 30));
    expect(getDocumentEmbeddingsMeta).not.toHaveBeenCalled();
  });

  it('fires fetch and returns data when enabled+docId set', async () => {
    vi.mocked(getDocumentEmbeddingsMeta).mockResolvedValue({
      docId: 'abc',
      model: 'bge-base-en-v1.5',
      dimensions: 768,
      totalChunks: 412,
      indexedAt: '2026-05-28T14:22:14Z',
      language: 'en',
    });
    const { result } = renderHook(() => useDocumentEmbeddingsMeta('abc', true), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.model).toBe('bge-base-en-v1.5');
    expect(getDocumentEmbeddingsMeta).toHaveBeenCalledWith('abc', expect.any(Object));
  });
});
