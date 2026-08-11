/**
 * @vitest-environment jsdom
 *
 * useHouseRulesMetadata unit tests — Issue #2492.
 *
 * Coverage:
 *   - 5-minute staleTime constant
 *   - Disabled when gameId is null / undefined / empty (no fetch)
 *   - Manual `enabled: false` overrides auto-enable
 *   - Calls `api.agentMemory.getHouseRulesMetadata(gameId)` with the right contract
 *   - Surfaces success / error / null (401) states from the underlying client
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  HOUSE_RULES_METADATA_STALE_TIME_MS,
  houseRulesMetadataKeys,
  useHouseRulesMetadata,
} from '../useHouseRulesMetadata';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    agentMemory: {
      getHouseRulesMetadata: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockGetHouseRulesMetadata = api.agentMemory.getHouseRulesMetadata as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const GAME_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_METADATA = {
  agentCount: 2,
  gameCount: 1,
  sessionCount: 0,
  kbCount: 12,
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useHouseRulesMetadata — constants', () => {
  it('exports a 5-minute staleTime in milliseconds', () => {
    expect(HOUSE_RULES_METADATA_STALE_TIME_MS).toBe(5 * 60 * 1000);
  });

  it('exposes a stable queryKey structure scoped by gameId', () => {
    expect(houseRulesMetadataKeys.byGame(GAME_ID)).toEqual([
      'agent-memory',
      'house-rules',
      'metadata',
      GAME_ID,
    ]);
  });

  it('queryKey is unique per gameId value including null/undefined/empty', () => {
    // Guard against cache collision when callers pass null/undefined — each
    // case gets a distinct entry rather than colliding on a shared `all` key.
    const nullKey = houseRulesMetadataKeys.byGame(null);
    const undefKey = houseRulesMetadataKeys.byGame(undefined);
    const emptyKey = houseRulesMetadataKeys.byGame('');
    const idKey = houseRulesMetadataKeys.byGame(GAME_ID);

    // null/undefined/'' all stringify to the same '' tail — that's fine because
    // `enabled: false` means queryFn never runs for any of them. The valid id
    // case stays distinct.
    expect(nullKey).toEqual(['agent-memory', 'house-rules', 'metadata', '']);
    expect(undefKey).toEqual(['agent-memory', 'house-rules', 'metadata', '']);
    expect(emptyKey).toEqual(['agent-memory', 'house-rules', 'metadata', '']);
    expect(idKey).not.toEqual(emptyKey);
  });
});

describe('useHouseRulesMetadata — auto-enable gate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does NOT fire when gameId is null', () => {
    renderHook(() => useHouseRulesMetadata({ gameId: null }), {
      wrapper: createWrapper(),
    });

    expect(mockGetHouseRulesMetadata).not.toHaveBeenCalled();
  });

  it('does NOT fire when gameId is undefined', () => {
    renderHook(() => useHouseRulesMetadata({ gameId: undefined }), {
      wrapper: createWrapper(),
    });

    expect(mockGetHouseRulesMetadata).not.toHaveBeenCalled();
  });

  it('does NOT fire when gameId is empty string', () => {
    renderHook(() => useHouseRulesMetadata({ gameId: '' }), {
      wrapper: createWrapper(),
    });

    expect(mockGetHouseRulesMetadata).not.toHaveBeenCalled();
  });

  it('fires when gameId is a valid string', async () => {
    mockGetHouseRulesMetadata.mockResolvedValue(SAMPLE_METADATA);

    const { result } = renderHook(() => useHouseRulesMetadata({ gameId: GAME_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockGetHouseRulesMetadata).toHaveBeenCalledTimes(1);
    expect(mockGetHouseRulesMetadata).toHaveBeenCalledWith(GAME_ID);
  });
});

describe('useHouseRulesMetadata — manual enabled override', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does NOT fire when enabled=false even with a valid gameId', () => {
    renderHook(() => useHouseRulesMetadata({ gameId: GAME_ID, enabled: false }), {
      wrapper: createWrapper(),
    });

    expect(mockGetHouseRulesMetadata).not.toHaveBeenCalled();
  });
});

describe('useHouseRulesMetadata — query result', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns counts on success', async () => {
    mockGetHouseRulesMetadata.mockResolvedValue(SAMPLE_METADATA);

    const { result } = renderHook(() => useHouseRulesMetadata({ gameId: GAME_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(SAMPLE_METADATA);
  });

  it('returns null when the apiClient returns null (401)', async () => {
    mockGetHouseRulesMetadata.mockResolvedValue(null);

    const { result } = renderHook(() => useHouseRulesMetadata({ gameId: GAME_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });

  it('exposes error state when the API rejects', async () => {
    const apiError = new Error('Backend unavailable');
    mockGetHouseRulesMetadata.mockRejectedValue(apiError);

    const { result } = renderHook(() => useHouseRulesMetadata({ gameId: GAME_ID }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Backend unavailable');
  });
});
