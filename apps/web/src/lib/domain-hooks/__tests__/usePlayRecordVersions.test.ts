import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, act, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';

import {
  usePlayRecordVersions,
  useRestorePlayRecordVersion,
  playRecordsKeys,
} from '../usePlayRecords';

// Mock the API client so tests don't hit the network
vi.mock('@/lib/api/play-records.api', () => ({
  playRecordsApi: {
    getVersions: vi.fn(),
    restoreVersion: vi.fn(),
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

const sampleVersions = [
  {
    versionNumber: 2,
    sessionDate: '2026-06-20',
    notes: 'Updated notes',
    location: null,
    createdAt: '2026-06-20T18:30:00Z',
    createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  },
  {
    versionNumber: 1,
    sessionDate: '2026-06-19',
    notes: null,
    location: 'Home',
    createdAt: '2026-06-19T10:00:00Z',
    createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  },
];

// ---- usePlayRecordVersions ----

describe('usePlayRecordVersions (#2437-3)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createClient();
    vi.clearAllMocks();
  });

  it('calls playRecordsApi.getVersions with the recordId and returns data', async () => {
    vi.mocked(playRecordsApi.getVersions).mockResolvedValue(sampleVersions as never);

    const { result } = renderHook(() => usePlayRecordVersions('rec-1'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(playRecordsApi.getVersions).toHaveBeenCalledWith('rec-1');
    expect(result.current.data).toEqual(sampleVersions);
  });

  it('uses playRecordsKeys.versions as the query key', async () => {
    vi.mocked(playRecordsApi.getVersions).mockResolvedValue(sampleVersions as never);

    const { result } = renderHook(() => usePlayRecordVersions('rec-42'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const cached = queryClient
      .getQueryCache()
      .find({ queryKey: playRecordsKeys.versions('rec-42') });
    expect(cached).toBeDefined();
  });

  it('is disabled when enabled=false', () => {
    const { result } = renderHook(() => usePlayRecordVersions('rec-1', false), {
      wrapper: makeWrapper(queryClient),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(playRecordsApi.getVersions).not.toHaveBeenCalled();
  });

  it('surfaces error when the API call fails', async () => {
    vi.mocked(playRecordsApi.getVersions).mockRejectedValue(new Error('Failed to load versions'));

    const { result } = renderHook(() => usePlayRecordVersions('rec-1'), {
      wrapper: makeWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Failed to load versions');
  });
});

// ---- useRestorePlayRecordVersion ----

describe('useRestorePlayRecordVersion (#2437-3)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createClient();
    vi.clearAllMocks();
  });

  it('calls playRecordsApi.restoreVersion with the recordId and versionNumber', async () => {
    vi.mocked(playRecordsApi.restoreVersion).mockResolvedValue(undefined);

    const { result } = renderHook(() => useRestorePlayRecordVersion('rec-5'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate(3);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(playRecordsApi.restoreVersion).toHaveBeenCalledWith('rec-5', 3);
  });

  it('invalidates detail and versions query keys on success', async () => {
    vi.mocked(playRecordsApi.restoreVersion).mockResolvedValue(undefined);
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useRestorePlayRecordVersion('rec-99'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate(1);
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: playRecordsKeys.detail('rec-99') })
    );
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: playRecordsKeys.versions('rec-99') })
    );
  });

  it('surfaces error when the API call fails', async () => {
    vi.mocked(playRecordsApi.restoreVersion).mockRejectedValue(
      new Error('Failed to restore version')
    );

    const { result } = renderHook(() => useRestorePlayRecordVersion('rec-5'), {
      wrapper: makeWrapper(queryClient),
    });

    await act(async () => {
      result.current.mutate(2);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('Failed to restore version');
  });
});
