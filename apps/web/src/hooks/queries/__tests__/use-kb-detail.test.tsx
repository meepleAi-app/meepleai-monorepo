import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { type ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import { server } from '@/__tests__/mocks/server';

import { useKbChunk, useKbChunks, useKbDocument, useSearchKbChunks } from '../use-kb-detail';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const DOC_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CHUNK_ID = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

const docFixture = {
  id: DOC_ID,
  title: 'Azul rulebook',
  docType: 'rulebook',
  gameId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  gameName: 'Azul',
  uploaderName: 'Marco',
  uploadedAt: '2026-01-12T00:00:00Z',
  lastIngestedAt: '2026-03-08T00:00:00Z',
  processingStatus: 'ready',
  chunkCount: 42,
  pageCount: 24,
  language: 'it',
  tags: [],
  fileSize: 1024,
  indexerVersion: 'v3',
};

describe('useKbDocument (#2311 FE-2)', () => {
  it('fetches the document by id and parses the response', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, () => HttpResponse.json(docFixture))
    );

    const { result } = renderHook(() => useKbDocument(DOC_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.title).toBe('Azul rulebook');
    expect(result.current.data?.chunkCount).toBe(42);
  });

  it('skips the request when id is empty (gate)', () => {
    const { result } = renderHook(() => useKbDocument(''), {
      wrapper: createWrapper(),
    });
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });
});

describe('useKbChunks (#2311 FE-2)', () => {
  const chunkFixture = {
    items: [
      {
        id: CHUNK_ID,
        position: 0,
        headingPath: ['Introduzione'],
        snippet: 'Benvenuto in Azul',
        pageNumber: 1,
        vectorId: CHUNK_ID.replace(/-/g, ''),
        usedInChats: 8,
      },
    ],
    nextCursor: null,
    totalCount: 1,
  };

  it('returns the chunks list including the BE-1 usedInChats counter', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks`, () => HttpResponse.json(chunkFixture))
    );

    const { result } = renderHook(() => useKbChunks(DOC_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.items[0].usedInChats).toBe(8);
  });

  it('appends limit + cursor to the query string', async () => {
    let observedSearch = '';
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks`, ({ request }) => {
        observedSearch = new URL(request.url).search;
        return HttpResponse.json({ items: [], nextCursor: null, totalCount: 0 });
      })
    );

    const { result } = renderHook(() => useKbChunks(DOC_ID, { limit: 5, cursor: 'eyJpIjoxfQ' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(observedSearch).toContain('limit=5');
    expect(observedSearch).toContain('cursor=');
  });
});

describe('useKbChunk (#2311 FE-2)', () => {
  it('fetches a single chunk when chunkId is provided', async () => {
    server.use(
      http.get(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks/${CHUNK_ID}`, () =>
        HttpResponse.json({
          id: CHUNK_ID,
          docId: DOC_ID,
          position: 0,
          headingPath: ['Introduzione'],
          content: '# Benvenuto\n\nMarkdown body',
          pageNumber: 1,
          prevChunkId: null,
          nextChunkId: null,
          metadata: {},
        })
      )
    );

    const { result } = renderHook(() => useKbChunk(DOC_ID, CHUNK_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.content).toContain('Markdown body');
  });

  it('skips the request when chunkId is empty (lazy preview-pane gate)', () => {
    const { result } = renderHook(() => useKbChunk(DOC_ID, ''), {
      wrapper: createWrapper(),
    });
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });
});

describe('useSearchKbChunks (#2311 FE-2)', () => {
  it('POSTs the search body and returns matches', async () => {
    let observedBody: unknown = null;
    server.use(
      http.post(`${API_BASE}/api/v1/kb-docs/${DOC_ID}/chunks/search`, async ({ request }) => {
        observedBody = await request.json();
        return HttpResponse.json({
          matches: [
            {
              chunkId: CHUNK_ID,
              headingPath: ['Setup'],
              snippet: 'piaze<mark>tte</mark>',
              rank: 0.9,
              pageNumber: 2,
              position: 5,
            },
          ],
          totalCount: 1,
          skip: 0,
          take: 20,
        });
      })
    );

    const { result } = renderHook(() => useSearchKbChunks({ docId: DOC_ID, query: 'piazette' }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(result.current.data?.matches).toHaveLength(1);
    expect(observedBody).toMatchObject({ query: 'piazette' });
  });

  it('skips the request when query is empty (debounced caller pattern)', () => {
    const { result } = renderHook(() => useSearchKbChunks({ docId: DOC_ID, query: '' }), {
      wrapper: createWrapper(),
    });
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });

  it('skips the request when query is whitespace-only', () => {
    const { result } = renderHook(() => useSearchKbChunks({ docId: DOC_ID, query: '   ' }), {
      wrapper: createWrapper(),
    });
    expect(result.current.isLoading).toBe(false);
  });
});
