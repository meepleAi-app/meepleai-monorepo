/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useAgentKbDocs, useAgentThreads, mapKbDocs, mapThreads } from '../useAgentData';

// ---------------------------------------------------------------------------
// Global fetch mock
// ---------------------------------------------------------------------------

const mockFetch = vi.fn();
global.fetch = mockFetch;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// ---------------------------------------------------------------------------
// mapKbDocs unit tests
// ---------------------------------------------------------------------------

describe('mapKbDocs', () => {
  it('normalises document fields and status', () => {
    const raw = [
      { id: 'd1', fileName: 'rules.pdf', uploadedAt: '2026-01-01T00:00:00Z', status: 'Indexed' },
      { id: 'd2', name: 'faq.pdf', createdAt: '2026-02-01T00:00:00Z', status: 'processing' },
      { id: 'd3', status: 'unknown' },
    ];
    const result = mapKbDocs(raw);

    expect(result).toHaveLength(3);
    expect(result[0]).toEqual({
      id: 'd1',
      fileName: 'rules.pdf',
      uploadedAt: '2026-01-01T00:00:00Z',
      status: 'indexed',
    });
    expect(result[1].fileName).toBe('faq.pdf');
    expect(result[1].uploadedAt).toBe('2026-02-01T00:00:00Z');
    expect(result[1].status).toBe('processing');
    // Unknown status falls back to 'none'
    expect(result[2].status).toBe('none');
    expect(result[2].fileName).toBe('Documento');
  });
});

// ---------------------------------------------------------------------------
// mapThreads unit tests
// ---------------------------------------------------------------------------

describe('mapThreads', () => {
  it('extracts first message preview and message count', () => {
    const raw = [
      {
        id: 't1',
        createdAt: '2026-03-01T00:00:00Z',
        messages: [{ content: 'Hello agent' }, { content: 'Reply' }],
      },
      {
        id: 't2',
        startedAt: '2026-03-02T00:00:00Z',
        messages: [],
      },
    ];
    const result = mapThreads(raw);

    expect(result).toHaveLength(2);
    expect(result[0]).toEqual({
      id: 't1',
      createdAt: '2026-03-01T00:00:00Z',
      messageCount: 2,
      firstMessagePreview: 'Hello agent',
    });
    expect(result[1].createdAt).toBe('2026-03-02T00:00:00Z');
    expect(result[1].messageCount).toBe(0);
    expect(result[1].firstMessagePreview).toBe('');
  });
});

// ---------------------------------------------------------------------------
// useAgentKbDocs hook tests
// ---------------------------------------------------------------------------

describe('useAgentKbDocs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns docs for a valid gameId', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [
        {
          id: 'doc-1',
          fileName: 'rules.pdf',
          status: 'Indexed',
          uploadedAt: '2026-01-01T00:00:00Z',
        },
      ],
    });

    const { result } = renderHook(() => useAgentKbDocs('game-123'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].id).toBe('doc-1');
    expect(result.current.data?.[0].status).toBe('indexed');
    expect(mockFetch).toHaveBeenCalledWith('/api/v1/knowledge-base/game-123/documents');
  });

  it('is disabled when gameId is empty', () => {
    const { result } = renderHook(() => useAgentKbDocs(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('is disabled when gameId is undefined', () => {
    const { result } = renderHook(() => useAgentKbDocs(undefined), {
      wrapper: createWrapper(),
    });

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('returns error when fetch fails', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 500 });

    const { result } = renderHook(() => useAgentKbDocs('game-err'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Failed to fetch KB docs');
  });
});

// ---------------------------------------------------------------------------
// useAgentThreads hook tests
// ---------------------------------------------------------------------------

describe('useAgentThreads', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns threads for a valid agentId', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [
        {
          id: 'thread-1',
          createdAt: '2026-03-19T10:00:00Z',
          messages: [{ content: 'How do I play?' }],
        },
      ],
    });

    const { result } = renderHook(() => useAgentThreads('agent-123'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].id).toBe('thread-1');
    expect(result.current.data?.[0].firstMessagePreview).toBe('How do I play?');
    expect(mockFetch).toHaveBeenCalledWith('/api/v1/chat-threads/my?agentId=agent-123');
  });

  it('is disabled when agentId is empty', () => {
    const { result } = renderHook(() => useAgentThreads(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('returns error when fetch fails', async () => {
    mockFetch.mockResolvedValueOnce({ ok: false, status: 404 });

    const { result } = renderHook(() => useAgentThreads('agent-err'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Failed to fetch threads');
  });
});
