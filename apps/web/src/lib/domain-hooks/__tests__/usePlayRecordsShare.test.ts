import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, act, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';

import {
  useGeneratePlayRecordShareToken,
  useRevokePlayRecordShareToken,
  useSharedPlayRecord,
  playRecordsKeys,
} from '../usePlayRecords';

// Mock the API client so tests don't hit the network
vi.mock('@/lib/api/play-records.api', () => ({
  playRecordsApi: {
    generateShareToken: vi.fn(),
    revokeShareToken: vi.fn(),
    getSharedRecord: vi.fn(),
  },
}));

import { playRecordsApi } from '@/lib/api/play-records.api';

function makeWrapper(queryClient: QueryClient) {
  return ({ children }: { children: ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
}

function createClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

// Minimal DTO enough to satisfy test assertions
const minimalDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameName: 'Codenames',
  sessionDate: '2026-06-21',
  status: 'Completed',
  players: [],
  visibility: 'Private',
  createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  gameId: null,
  duration: null,
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  startTime: null,
  endTime: null,
  notes: null,
  location: null,
  createdAt: '2026-06-21T10:00:00Z',
  updatedAt: '2026-06-21T10:00:00Z',
};

describe('useGeneratePlayRecordShareToken (#2437-2)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createClient();
    vi.clearAllMocks();
  });

  it('calls playRecordsApi.generateShareToken with the recordId and returns data', async () => {
    const payload = { shareToken: 'tok1', shareUrl: 'https://app.meeple.ai/shared/tok1' };
    vi.mocked(playRecordsApi.generateShareToken).mockResolvedValue(payload);

    const { result } = renderHook(() => useGeneratePlayRecordShareToken('rec-1'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(playRecordsApi.generateShareToken).toHaveBeenCalledWith('rec-1');
    expect(result.current.data).toEqual(payload);
  });

  it('invalidates the detail query key on success', async () => {
    vi.mocked(playRecordsApi.generateShareToken).mockResolvedValue({
      shareToken: 'tok2',
      shareUrl: 'https://app.meeple.ai/shared/tok2',
    });
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useGeneratePlayRecordShareToken('rec-42'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: playRecordsKeys.detail('rec-42') })
    );
  });

  it('surfaces error when the API call fails', async () => {
    vi.mocked(playRecordsApi.generateShareToken).mockRejectedValue(
      new Error('Failed to generate share link')
    );

    const { result } = renderHook(() => useGeneratePlayRecordShareToken('rec-1'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Failed to generate share link');
  });
});

describe('useRevokePlayRecordShareToken (#2437-2)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createClient();
    vi.clearAllMocks();
  });

  it('calls playRecordsApi.revokeShareToken with the recordId', async () => {
    vi.mocked(playRecordsApi.revokeShareToken).mockResolvedValue(undefined);

    const { result } = renderHook(() => useRevokePlayRecordShareToken('rec-2'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(playRecordsApi.revokeShareToken).toHaveBeenCalledWith('rec-2');
  });

  it('invalidates the detail query key on success', async () => {
    vi.mocked(playRecordsApi.revokeShareToken).mockResolvedValue(undefined);
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useRevokePlayRecordShareToken('rec-99'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: playRecordsKeys.detail('rec-99') })
    );
  });
});

describe('useSharedPlayRecord (#2437-2)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createClient();
    vi.clearAllMocks();
  });

  it('fetches the shared record by token', async () => {
    vi.mocked(playRecordsApi.getSharedRecord).mockResolvedValue(minimalDto as never);

    const { result } = renderHook(() => useSharedPlayRecord('abc123'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(playRecordsApi.getSharedRecord).toHaveBeenCalledWith('abc123');
    expect(result.current.data).toEqual(minimalDto);
  });

  it('uses playRecordsKeys.shared as the query key', async () => {
    vi.mocked(playRecordsApi.getSharedRecord).mockResolvedValue(minimalDto as never);

    const { result } = renderHook(() => useSharedPlayRecord('mytoken'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const cached = queryClient
      .getQueryCache()
      .find({ queryKey: playRecordsKeys.shared('mytoken') });
    expect(cached).toBeDefined();
  });

  it('is disabled when token is empty string', () => {
    const { result } = renderHook(() => useSharedPlayRecord(''), {
      wrapper: makeWrapper(queryClient),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(playRecordsApi.getSharedRecord).not.toHaveBeenCalled();
  });

  it('surfaces error when the API call fails', async () => {
    vi.mocked(playRecordsApi.getSharedRecord).mockRejectedValue(
      new Error('Shared play record not found')
    );

    const { result } = renderHook(() => useSharedPlayRecord('badtoken'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Shared play record not found');
  });
});
