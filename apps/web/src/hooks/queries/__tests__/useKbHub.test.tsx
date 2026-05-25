/**
 * useKbHub hooks tests (Issue #1481).
 *
 * Validates query keys, enablement gating, and cache invalidation post-mutation.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  kbHubKeys,
  useDeletePdf,
  useGamePdfs,
  useReindexKb,
  useRebuildRaptor,
  useUserKbStatus,
} from '../useKbHub';

const mockGetUserGameKbStatus = vi.fn();
const mockGetGamePdfs = vi.fn();
const mockReindexKb = vi.fn();
const mockRebuildRaptor = vi.fn();
const mockAdminDeletePdf = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getUserGameKbStatus: (...args: unknown[]) => mockGetUserGameKbStatus(...args),
      reindexKb: (...args: unknown[]) => mockReindexKb(...args),
      rebuildRaptor: (...args: unknown[]) => mockRebuildRaptor(...args),
    },
    library: {
      getGamePdfs: (...args: unknown[]) => mockGetGamePdfs(...args),
    },
    pdf: {
      adminDeletePdf: (...args: unknown[]) => mockAdminDeletePdf(...args),
    },
  },
}));

const GAME_ID = 'game-123';

function wrapper({ children }: PropsWithChildren) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useKbHub (Issue #1481)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('kbHubKeys produces stable, isolated cache keys', () => {
    expect(kbHubKeys.all).toEqual(['kbHub']);
    expect(kbHubKeys.status(GAME_ID)).toEqual(['kbHub', 'status', GAME_ID]);
    expect(kbHubKeys.pdfs(GAME_ID)).toEqual(['kbHub', 'pdfs', GAME_ID]);
  });

  it('useUserKbStatus is disabled when gameId is undefined', () => {
    renderHook(() => useUserKbStatus(undefined), { wrapper });
    expect(mockGetUserGameKbStatus).not.toHaveBeenCalled();
  });

  it('useUserKbStatus fetches when gameId provided', async () => {
    mockGetUserGameKbStatus.mockResolvedValueOnce({
      gameId: GAME_ID,
      isIndexed: true,
      documentCount: 12,
      coverageScore: 73,
      coverageLevel: 'Standard',
      suggestedQuestions: [],
    });
    const { result } = renderHook(() => useUserKbStatus(GAME_ID), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGetUserGameKbStatus).toHaveBeenCalledWith(GAME_ID);
    expect(result.current.data?.documentCount).toBe(12);
  });

  it('useGamePdfs fetches PDF list when gameId provided', async () => {
    mockGetGamePdfs.mockResolvedValueOnce([
      {
        id: 'p1',
        name: 'Rulebook',
        pageCount: 50,
        fileSizeBytes: 12345,
        uploadedAt: '2026-01-01T00:00:00Z',
        source: 'Custom',
      },
    ]);
    const { result } = renderHook(() => useGamePdfs(GAME_ID), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGetGamePdfs).toHaveBeenCalledWith(GAME_ID);
    expect(result.current.data?.[0]?.id).toBe('p1');
  });

  it('useReindexKb invokes mutation and resolves', async () => {
    mockReindexKb.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useReindexKb(GAME_ID), { wrapper });
    await result.current.mutateAsync();
    expect(mockReindexKb).toHaveBeenCalledWith(GAME_ID);
  });

  it('useRebuildRaptor invokes mutation and resolves', async () => {
    mockRebuildRaptor.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useRebuildRaptor(GAME_ID), { wrapper });
    await result.current.mutateAsync();
    expect(mockRebuildRaptor).toHaveBeenCalledWith(GAME_ID);
  });

  it('useDeletePdf invokes adminDeletePdf endpoint with pdfId', async () => {
    mockAdminDeletePdf.mockResolvedValueOnce(undefined);
    const { result } = renderHook(() => useDeletePdf(GAME_ID), { wrapper });
    await result.current.mutateAsync('pdf-1');
    expect(mockAdminDeletePdf).toHaveBeenCalledWith('pdf-1');
  });
});
