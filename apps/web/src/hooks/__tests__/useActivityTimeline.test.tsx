/**
 * useActivityTimeline Tests (Issue #3925)
 *
 * Test coverage for React Query hook and API fetching.
 * Tests: mock data, filtering, search, pagination, query keys.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  useActivityTimeline,
  fetchActivityTimeline,
  activityTimelineKeys,
  type ActivityTimelineRequest,
} from '../useActivityTimeline';

// ============================================================================
// Test Setup
// ============================================================================

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
}

const defaultParams: ActivityTimelineRequest = {
  types: [],
  search: '',
  skip: 0,
  take: 20,
  order: 'desc',
};

// ============================================================================
// Tests
// ============================================================================

describe('fetchActivityTimeline', () => {
  describe('Mock Data Mode', () => {
    it('returns items when no filters applied', async () => {
      const result = await fetchActivityTimeline(defaultParams);

      expect(result.items).toBeDefined();
      expect(result.items.length).toBeGreaterThan(0);
      expect(result.totalCount).toBeGreaterThan(0);
    });

    it('filters by type', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        types: ['game_added'],
      });

      expect(result.items.every((item) => item.type === 'game_added')).toBe(true);
    });

    it('filters by multiple types', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        types: ['game_added', 'session_completed'],
      });

      expect(
        result.items.every(
          (item) => item.type === 'game_added' || item.type === 'session_completed'
        )
      ).toBe(true);
    });

    it('filters by search on gameName', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        search: 'wingspan',
      });

      expect(result.items.length).toBeGreaterThan(0);
      expect(
        result.items.every(
          (item) =>
            item.gameName?.toLowerCase().includes('wingspan') ||
            item.topic?.toLowerCase().includes('wingspan')
        )
      ).toBe(true);
    });

    it('filters by search on topic', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        search: 'strategie',
      });

      expect(result.items.length).toBeGreaterThan(0);
      expect(
        result.items.every(
          (item) =>
            item.gameName?.toLowerCase().includes('strategie') ||
            item.topic?.toLowerCase().includes('strategie')
        )
      ).toBe(true);
    });

    it('combines type and search filters', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        types: ['chat_saved'],
        search: 'regole',
      });

      expect(
        result.items.every(
          (item) =>
            item.type === 'chat_saved' &&
            (item.gameName?.toLowerCase().includes('regole') ||
              item.topic?.toLowerCase().includes('regole'))
        )
      ).toBe(true);
    });

    it('returns empty when no matches', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        search: 'nonexistent_game_xyz',
      });

      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
    });

    it('sorts by desc order (default)', async () => {
      const result = await fetchActivityTimeline(defaultParams);

      for (let i = 1; i < result.items.length; i++) {
        const prev = new Date(result.items[i - 1].timestamp).getTime();
        const curr = new Date(result.items[i].timestamp).getTime();
        expect(prev).toBeGreaterThanOrEqual(curr);
      }
    });

    it('sorts by asc order', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        order: 'asc',
      });

      for (let i = 1; i < result.items.length; i++) {
        const prev = new Date(result.items[i - 1].timestamp).getTime();
        const curr = new Date(result.items[i].timestamp).getTime();
        expect(prev).toBeLessThanOrEqual(curr);
      }
    });

    it('respects take limit', async () => {
      const result = await fetchActivityTimeline({
        ...defaultParams,
        take: 3,
      });

      expect(result.items.length).toBeLessThanOrEqual(3);
    });

    it('returns hasMore flag correctly', async () => {
      const allResults = await fetchActivityTimeline(defaultParams);
      const limitedResults = await fetchActivityTimeline({
        ...defaultParams,
        take: 3,
      });

      if (allResults.totalCount > 3) {
        expect(limitedResults.hasMore).toBe(true);
      }
    });
  });
});

describe('activityTimelineKeys', () => {
  it('generates base key', () => {
    expect(activityTimelineKeys.all).toEqual(['activity-timeline']);
  });

  it('generates filtered key with params', () => {
    const params: ActivityTimelineRequest = {
      types: ['game_added'],
      search: 'catan',
      skip: 0,
      take: 20,
      order: 'desc',
    };

    const key = activityTimelineKeys.filtered(params);
    expect(key[0]).toBe('activity-timeline');
    expect(key[1]).toEqual(params);
  });

  it('generates different keys for different params', () => {
    const key1 = activityTimelineKeys.filtered({ ...defaultParams, search: 'a' });
    const key2 = activityTimelineKeys.filtered({ ...defaultParams, search: 'b' });

    expect(JSON.stringify(key1)).not.toEqual(JSON.stringify(key2));
  });
});

describe('useActivityTimeline', () => {
  it('fetches timeline data', async () => {
    const { result } = renderHook(
      () => useActivityTimeline(defaultParams),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items.length).toBeGreaterThan(0);
    expect(result.current.data?.totalCount).toBeGreaterThan(0);
  });

  it('returns filtered data when types provided', async () => {
    const params: ActivityTimelineRequest = {
      ...defaultParams,
      types: ['game_added'],
    };

    const { result } = renderHook(
      () => useActivityTimeline(params),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items.every((i) => i.type === 'game_added')).toBe(true);
  });

  it('returns filtered data when search provided', async () => {
    const params: ActivityTimelineRequest = {
      ...defaultParams,
      search: 'wingspan',
    };

    const { result } = renderHook(
      () => useActivityTimeline(params),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.items.length).toBeGreaterThan(0);
  });
});
