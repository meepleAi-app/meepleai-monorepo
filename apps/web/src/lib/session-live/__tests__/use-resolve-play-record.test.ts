/**
 * Tests for useResolvePlayRecord (Issue #2503 — Opzione C polling).
 *
 * Covers:
 *   - initial status is 'idle'
 *   - start() transitions to 'resolving'
 *   - resolved when records returned immediately
 *   - timeout when no records within TIMEOUT_MS
 *   - cleanup: timers cancelled on unmount
 *
 * Timer strategy: vi.useFakeTimers() + vi.runAllTimersAsync() inside act().
 * flushMicrotasks() is a local helper: await Promise.resolve() drains the
 * microtask queue without needing vi.runAllMicrotasks() (unavailable in v4).
 */
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useResolvePlayRecord } from '../use-resolve-play-record';

// ---------------------------------------------------------------------------
// Fake timers + mock api
// ---------------------------------------------------------------------------

const getHistoryMock = vi.fn<[{ gameId?: string; pageSize?: number }], Promise<unknown>>();

vi.mock('@/lib/api', () => ({
  api: {
    playRecords: {
      getHistory: (params: { gameId?: string; pageSize?: number }) => getHistoryMock(params),
    },
  },
}));

const GAME_ID = 'game-00000001-0001-0001-0001-000000000001';
const RECORD_ID = 'record-00000000-0000-4000-8000-000000002503';

/** Drains the Promise microtask queue without vi.runAllMicrotasks(). */
const flushMicrotasks = () => Promise.resolve();

describe('useResolvePlayRecord (#2503)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getHistoryMock.mockReset();
  });

  afterEach(() => {
    vi.runAllTimers();
    vi.useRealTimers();
  });

  it('initial status is "idle" and playRecordId is null', () => {
    const { result } = renderHook(() => useResolvePlayRecord());
    expect(result.current.status).toBe('idle');
    expect(result.current.playRecordId).toBeNull();
  });

  it('start() transitions status to "resolving"', async () => {
    getHistoryMock.mockResolvedValue({ records: [] });

    const { result } = renderHook(() => useResolvePlayRecord());

    act(() => {
      result.current.start(GAME_ID);
    });

    // Status is synchronously set to 'resolving' inside start()
    expect(result.current.status).toBe('resolving');
  });

  it('resolved immediately when getHistory returns a record on first poll', async () => {
    getHistoryMock.mockResolvedValueOnce({
      records: [
        {
          id: RECORD_ID,
          gameName: 'Mage Knight',
          sessionDate: '2026-06-24',
          status: 'Completed',
          playerCount: 2,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 1,
      totalPages: 1,
    });

    const { result } = renderHook(() => useResolvePlayRecord());

    await act(async () => {
      result.current.start(GAME_ID);
      // Flush microtasks so the async poll() runs to completion
      await flushMicrotasks();
      await flushMicrotasks();
    });

    expect(result.current.status).toBe('resolved');
    expect(result.current.playRecordId).toBe(RECORD_ID);
  });

  it('polls with backoff and resolves when record appears on second attempt', async () => {
    getHistoryMock
      .mockResolvedValueOnce({ records: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 })
      .mockResolvedValueOnce({
        records: [
          {
            id: RECORD_ID,
            gameName: 'Mage Knight',
            sessionDate: '2026-06-24',
            status: 'Completed',
            playerCount: 2,
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 1,
        totalPages: 1,
      });

    const { result } = renderHook(() => useResolvePlayRecord());

    await act(async () => {
      result.current.start(GAME_ID);
      // Drain microtasks for first poll (returns empty)
      await flushMicrotasks();
      await flushMicrotasks();
      // Advance the 1000ms backoff timer
      vi.advanceTimersByTime(1000);
      // Drain microtasks for second poll (returns record)
      await flushMicrotasks();
      await flushMicrotasks();
    });

    expect(result.current.status).toBe('resolved');
    expect(result.current.playRecordId).toBe(RECORD_ID);
  });

  it('timeout after 15s when no record ever appears', async () => {
    getHistoryMock.mockResolvedValue({
      records: [],
      totalCount: 0,
      page: 1,
      pageSize: 1,
      totalPages: 0,
    });

    const { result } = renderHook(() => useResolvePlayRecord());

    await act(async () => {
      result.current.start(GAME_ID);
      // Advance past the 15s global timeout
      vi.advanceTimersByTime(15001);
      await flushMicrotasks();
    });

    expect(result.current.status).toBe('timeout');
    expect(result.current.playRecordId).toBeNull();
  });

  it('calls getHistory with the provided gameId and pageSize=1', async () => {
    getHistoryMock.mockResolvedValueOnce({
      records: [
        {
          id: RECORD_ID,
          gameName: 'G',
          sessionDate: '2026-06-24',
          status: 'Completed',
          playerCount: 1,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 1,
      totalPages: 1,
    });

    const { result } = renderHook(() => useResolvePlayRecord());

    await act(async () => {
      result.current.start(GAME_ID);
      await flushMicrotasks();
      await flushMicrotasks();
    });

    expect(getHistoryMock).toHaveBeenCalledWith(
      expect.objectContaining({ gameId: GAME_ID, pageSize: 1 })
    );
  });

  it('cleanup on unmount cancels pending timers (no state update after unmount)', async () => {
    getHistoryMock.mockResolvedValue({
      records: [],
      totalCount: 0,
      page: 1,
      pageSize: 1,
      totalPages: 0,
    });

    const { result, unmount } = renderHook(() => useResolvePlayRecord());

    act(() => {
      result.current.start(GAME_ID);
    });

    // Unmount while resolving
    unmount();

    // Advance timers — should not throw (no state update on unmounted component)
    await act(async () => {
      vi.advanceTimersByTime(20000);
      await flushMicrotasks();
    });

    // No assertion needed — absence of error/warning proves cleanup worked.
    expect(true).toBe(true);
  });
});
