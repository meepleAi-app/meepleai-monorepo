/**
 * useGameKbStatus Hook Tests — Game Night MVP Hardening Task 1.1
 */

import { createElement, type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import { useGameKbStatus } from '../useGameKbStatus';

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getUserGameKbStatus: vi.fn(),
    },
  },
}));

const getUserGameKbStatusMock = vi.mocked(api.knowledgeBase.getUserGameKbStatus);

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return createElement(QueryClientProvider, { client: qc }, children);
}

describe('useGameKbStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns isIndexed=true when KB has documents', async () => {
    getUserGameKbStatusMock.mockResolvedValue({
      gameId: 'g1',
      isIndexed: true,
      documentCount: 3,
      coverageScore: 80,
      coverageLevel: 'Standard',
      suggestedQuestions: ['How many players?', 'Setup time?'],
    });

    const { result } = renderHook(() => useGameKbStatus('g1'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isIndexed).toBe(true);
    expect(result.current.documentCount).toBe(3);
    expect(result.current.coverageLevel).toBe('Standard');
    expect(result.current.suggestedQuestions).toEqual(['How many players?', 'Setup time?']);
  });

  it('returns isIndexed=false on empty KB', async () => {
    getUserGameKbStatusMock.mockResolvedValue({
      gameId: 'g2',
      isIndexed: false,
      documentCount: 0,
      coverageScore: 0,
      coverageLevel: 'None',
      suggestedQuestions: [],
    });

    const { result } = renderHook(() => useGameKbStatus('g2'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isIndexed).toBe(false);
    expect(result.current.documentCount).toBe(0);
    expect(result.current.coverageLevel).toBe('None');
  });

  it('returns safe defaults when client returns null', async () => {
    getUserGameKbStatusMock.mockResolvedValue(null);

    const { result } = renderHook(() => useGameKbStatus('g3'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isIndexed).toBe(false);
    expect(result.current.documentCount).toBe(0);
    expect(result.current.coverageLevel).toBe('None');
  });

  it('skips fetch when gameId is null', () => {
    const { result } = renderHook(() => useGameKbStatus(null), { wrapper });
    expect(getUserGameKbStatusMock).not.toHaveBeenCalled();
    expect(result.current.isIndexed).toBe(false);
    expect(result.current.documentCount).toBe(0);
    expect(result.current.coverageLevel).toBe('None');
    expect(result.current.suggestedQuestions).toEqual([]);
    expect(result.current.isLoading).toBe(false);
  });

  it('skips fetch when gameId is undefined (legacy consumer compatibility)', () => {
    const { result } = renderHook(() => useGameKbStatus(undefined), { wrapper });
    expect(getUserGameKbStatusMock).not.toHaveBeenCalled();
    expect(result.current.isIndexed).toBe(false);
  });
});
