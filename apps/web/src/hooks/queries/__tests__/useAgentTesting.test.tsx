/**
 * useAgentTesting Hook Tests
 * Issue #4673: Agent testing mutation hooks coverage.
 *
 * Tests:
 * - useAgentAutoTest: success, error, toast on error, correct endpoint
 * - useAskAgentQuestion: success, error, toast on error, correct payload
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import type { ReactNode } from 'react';

import { useAgentAutoTest, useAskAgentQuestion } from '../useAgentTesting';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  getApiBase: vi.fn(() => ''),
}));

const { mockToastError } = vi.hoisted(() => ({
  mockToastError: vi.fn(),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: mockToastError,
  },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      mutations: { retry: false },
      queries: { retry: false, gcTime: 0 },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

const mockAutoTestResult = {
  gameId: 'game-uuid-1',
  gameTitle: 'Gloomhaven',
  testCases: Array.from({ length: 8 }, (_, i) => ({
    index: i,
    question: `Question ${i}`,
    answer: 'Some answer',
    confidenceScore: 0.85,
    latencyMs: 500,
    chunksRetrieved: 3,
    passed: true,
    failureReason: null,
  })),
  report: {
    totalTests: 8,
    passed: 8,
    failed: 0,
    averageConfidence: 0.85,
    averageLatencyMs: 500,
    overallGrade: 'A',
    passRate: 1.0,
  },
  executedAt: '2026-02-21T10:00:00Z',
};

const mockChatResponse = {
  answer: 'Setup involves placing tiles on the board.',
  retrievedChunks: [
    {
      content: 'Place the board in the center.',
      relevanceScore: 0.9,
      chunkIndex: 0,
      metadata: {},
    },
  ],
  latencyMs: 350,
  tokenUsage: {
    promptTokens: 100,
    completionTokens: 50,
    totalTokens: 150,
    embeddingTokens: 10,
  },
};

// ─── Tests: useAgentAutoTest ──────────────────────────────────────────────────

describe('useAgentAutoTest', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('should call the correct endpoint with gameId in URL', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockAutoTestResult),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('game-uuid-1');

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/admin/games/game-uuid-1/agent/auto-test'),
      expect.any(Object)
    );
  });

  it('should use POST method with credentials: include', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockAutoTestResult),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('game-uuid-1');

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ method: 'POST', credentials: 'include' })
    );
  });

  it('should return the auto-test result on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockAutoTestResult),
    }));

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('game-uuid-1');

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.gameId).toBe('game-uuid-1');
    expect(result.current.data?.report.overallGrade).toBe('A');
    expect(result.current.data?.testCases).toHaveLength(8);
  });

  it('should enter error state on HTTP failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: () => Promise.resolve({ detail: 'Auto-test service unavailable' }),
    }));

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('game-uuid-1');

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toContain('Auto-test service unavailable');
  });

  it('should show error toast on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      json: () => Promise.resolve({ detail: 'Game not found' }),
    }));

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('missing-game');

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(mockToastError).toHaveBeenCalled();
  });

  it('should handle fetch failure gracefully', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Network error')));

    const { result } = renderHook(() => useAgentAutoTest(), {
      wrapper: createWrapper(),
    });

    result.current.mutate('game-uuid-1');

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Network error');
  });
});

// ─── Tests: useAskAgentQuestion ───────────────────────────────────────────────

describe('useAskAgentQuestion', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('should call the correct endpoint', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockChatResponse),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useAskAgentQuestion(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', question: 'How do you set up the game?' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/agents/chat/ask'),
      expect.any(Object)
    );
  });

  it('should send question, strategy, and gameId in body', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockChatResponse),
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useAskAgentQuestion(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', question: 'How do you win?' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        body: JSON.stringify({
          question: 'How do you win?',
          strategy: 1,
          gameId: 'game-uuid',
        }),
      })
    );
  });

  it('should return the chat response on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockChatResponse),
    }));

    const { result } = renderHook(() => useAskAgentQuestion(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', question: 'Win conditions?' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.answer).toBe('Setup involves placing tiles on the board.');
    expect(result.current.data?.retrievedChunks).toHaveLength(1);
  });

  it('should show error toast on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      json: () => Promise.resolve({ detail: 'Agent service unavailable' }),
    }));

    const { result } = renderHook(() => useAskAgentQuestion(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', question: 'question' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(mockToastError).toHaveBeenCalled();
  });

  it('should enter error state on network failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const { result } = renderHook(() => useAskAgentQuestion(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', question: 'something' });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
